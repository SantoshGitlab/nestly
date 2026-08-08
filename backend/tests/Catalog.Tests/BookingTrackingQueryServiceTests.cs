using System.Text.Json;
using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Tracking;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 275: consumer-api's GET /bookings/{bookingId}/tracking.
///
/// Runs the real service over the real repositories on SQLite (see
/// <see cref="TestDatabase"/>) rather than through HTTP - this codebase has
/// no WebApplicationFactory harness, and the controller action is a two-line
/// pass-through to <c>GetForCustomerAsync</c> plus <c>ToProblemResult</c>,
/// which maps ErrorType.NotFound to 404 and ErrorType.Forbidden to 403. So
/// asserting the ErrorType here is asserting the status code, and the tests
/// below say so explicitly where it matters.
///
/// SQLite/PostgreSQL divergence: the runtime database is PostgreSQL and the
/// schema here comes from EnsureCreated off the EF model, never migrations.
/// Nothing this endpoint reads depends on a provider-specific type - the
/// coordinates are decimal, the timestamps are UTC DateTime, and every read
/// is by primary key or by an indexed booking id - so the two agree on
/// everything these tests assert.
/// </summary>
public sealed class BookingTrackingQueryServiceTests : IClassFixture<TestDatabase>
{
    /// <summary>
    /// The two tests that assert on masking pin their own number, because the
    /// expected masked form depends on it. Every other test takes a generated
    /// one: Provider.Phone is uniquely indexed and this class shares a single
    /// SQLite database across its tests, so a fixed number everywhere would
    /// make the second test to seed a provider fail on the index rather than
    /// on anything it is asserting.
    /// </summary>
    private const string MaskingSubjectPhone = "+919812346275";

    /// <summary>A second distinct number, so the leak test cannot pass on a value another test happened to seed.</summary>
    private const string LeakSubjectPhone = "+919887776655";

    /// <summary>
    /// Mirrors ASP.NET Core MVC's own defaults: consumer-api registers no
    /// AddJsonOptions and no JsonStringEnumConverter, so its responses are
    /// serialized web-style (camelCase, enums as ordinals). Serializing with
    /// these options is what makes the leak test a statement about the wire,
    /// not about the DTO.
    /// </summary>
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TestDatabase _db;

    public BookingTrackingQueryServiceTests(TestDatabase db) => _db = db;

    private static BookingTrackingQueryService Service(NestlyDbContext context) =>
        new(new BookingRepository(context),
            new BookingProviderAssignmentRepository(context),
            new ProviderRepository(context),
            new ProviderLocationPingRepository(context),
            new BookingTrackingRepository(context),
            new ReviewRepository(context));

    /// <summary>A customer, and their booking walked up the lifecycle to <paramref name="status"/>.</summary>
    private static Booking SeedBooking(NestlyDbContext context, Guid customerId, BookingStatus status)
    {
        var customer = new Customer(customerId, "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active);
        context.Add(customer);

        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot("Test Customer", customer.Mobile),
            null,
            new AddressSnapshot("Home", "123 St", null, null, "560001", "Bengaluru", "Karnataka", 12.9m, 77.5m, "Test", "9000000000"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));

        foreach (var step in PathTo(status))
        {
            booking.TransitionTo(step, "test");
        }

        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    /// <summary>The legal transition chain to <paramref name="status"/> - BookingLifecycle is the authority, a test may not shortcut it.</summary>
    private static IEnumerable<BookingStatus> PathTo(BookingStatus status)
    {
        BookingStatus[] toAssigned =
        [
            BookingStatus.PaymentPending, BookingStatus.Confirmed,
            BookingStatus.AwaitingFulfilment, BookingStatus.Assigned
        ];

        return status switch
        {
            BookingStatus.Assigned => toAssigned,
            BookingStatus.ProviderEnRoute => [.. toAssigned, BookingStatus.ProviderEnRoute],
            BookingStatus.InProgress => [.. toAssigned, BookingStatus.InProgress],
            BookingStatus.Completed => [.. toAssigned, BookingStatus.InProgress, BookingStatus.Completed],
            BookingStatus.CancelledByCustomer => [.. toAssigned, BookingStatus.CancelledByCustomer],
            BookingStatus.AwaitingFulfilment => toAssigned[..3],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "This test helper has no path to that status.")
        };
    }

    private static string UniquePhone() => "+9198" + Guid.NewGuid().ToString("N")[..8];

    private static Provider SeedProvider(NestlyDbContext context, string? phone = null)
    {
        phone ??= UniquePhone();
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, phone);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(provider);
        context.SaveChanges();
        return provider;
    }

    private static void SeedAssignment(NestlyDbContext context, Booking booking, Provider provider)
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), booking.Id, provider.Id, BookingAssignedByType.System, null, null);
        assignment.Accept();
        context.Add(assignment);
        context.SaveChanges();
    }

    private static void SeedPing(NestlyDbContext context, Booking booking, Provider provider, decimal lat, decimal lng, DateTime recordedAtUtc)
    {
        context.Add(new ProviderLocationPing(
            Guid.NewGuid(), provider.Id, booking.Id, lat, lng, accuracyMetres: 12m,
            recordedAtUtc: recordedAtUtc, receivedAtUtc: recordedAtUtc.AddSeconds(1)));
        context.SaveChanges();
    }

    private static void SeedEta(NestlyDbContext context, Booking booking, Provider provider, int etaSeconds, DateTime computedAtUtc)
    {
        var tracking = new BookingTracking(Guid.NewGuid(), booking.Id);
        tracking.ApplyEta(provider.Id, etaSeconds, distanceMetres: 3200, BookingEtaSource.Sandbox, 12.95m, 77.61m, computedAtUtc);
        context.Add(tracking);
        context.SaveChanges();
    }

    // ---- Happy path ------------------------------------------------------

    [Fact]
    public async Task Returns_everything_the_tracking_screen_needs()
    {
        var customerId = Guid.NewGuid();
        var recordedAt = new DateTime(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc);
        var computedAt = new DateTime(2026, 8, 7, 9, 30, 5, DateTimeKind.Utc);

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context, MaskingSubjectPhone);
            SeedAssignment(context, booking, provider);
            SeedPing(context, booking, provider, 12.95m, 77.61m, recordedAt);
            SeedEta(context, booking, provider, etaSeconds: 480, computedAt);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsSuccess.Should().BeTrue();
        var tracking = result.Value;

        tracking.BookingId.Should().Be(bookingId);
        tracking.Status.Should().Be(BookingStatus.ProviderEnRoute);
        tracking.StatusLabel.Should().Be("On the way", "the label comes from BookingStatusMapper, never a second copy of the mapping");

        tracking.Provider.Should().NotBeNull();
        tracking.Provider!.DisplayName.Should().Be("Ravi's Repairs");
        tracking.Provider.MaskedPhone.Should().Be("*********6275");

        tracking.ProviderLocation.Should().NotBeNull();
        tracking.ProviderLocation!.Latitude.Should().Be(12.95m);
        tracking.ProviderLocation.Longitude.Should().Be(77.61m);
        tracking.ProviderLocation.RecordedAtUtc.Should().Be(recordedAt);

        tracking.Eta.Should().NotBeNull();
        tracking.Eta!.EtaSeconds.Should().Be(480);
        tracking.Eta.EtaComputedAtUtc.Should().Be(computedAt);

        // The destination is the booking's own immutable snapshot, so an edit
        // to the customer's address record cannot move the pin mid-job.
        tracking.Destination.Latitude.Should().Be(12.9m);
        tracking.Destination.Longitude.Should().Be(77.5m);
    }

    [Fact]
    public async Task Returns_the_latest_fix_not_an_older_one_from_the_trail()
    {
        var customerId = Guid.NewGuid();
        var older = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 8, 7, 9, 10, 0, DateTimeKind.Utc);

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            SeedPing(context, booking, provider, 12.90m, 77.50m, older);
            SeedPing(context, booking, provider, 12.96m, 77.62m, newer);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.Value.ProviderLocation!.RecordedAtUtc.Should().Be(newer);
        result.Value.ProviderLocation.Latitude.Should().Be(12.96m);
    }

    // ---- The phone is masked, on the wire --------------------------------

    /// <summary>
    /// The requirement that actually matters, asserted against the serialized
    /// payload rather than the DTO: a property added to the response carrying
    /// the raw number would satisfy every field-level assertion above and
    /// still be a PII leak. This test reads the bytes a client would receive.
    /// </summary>
    [Fact]
    public async Task Never_serializes_the_raw_provider_phone_number()
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context, LeakSubjectPhone);
            SeedAssignment(context, booking, provider);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        var json = JsonSerializer.Serialize(result.Value, ApiJsonOptions);

        json.Should().NotContain(LeakSubjectPhone, "the raw number must never reach the wire in any field");
        json.Should().NotContain("988777", "nor may any readable fragment of it beyond the last four digits");
        json.Should().NotContain("+91", "the country code is part of the number and is masked with it");
        json.Should().Contain("*********6655", "the masked form is what the screen shows");
    }

    /// <summary>
    /// The other half of "nothing more": the response must not quietly widen
    /// into a booking read. Asserted on the serialized property names so that
    /// adding a field to the record - the likely way this erodes - fails here
    /// rather than shipping.
    /// </summary>
    [Fact]
    public async Task Carries_only_the_tracking_fields_and_no_other_booking_data()
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, ApiJsonOptions));
        var topLevel = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        topLevel.Should().BeEquivalentTo(
            ["bookingId", "status", "statusLabel", "provider", "providerLocation", "eta", "destination"],
            "this response is PII-bounded - anything else a screen needs belongs on the booking detail");

        var providerFields = document.RootElement.GetProperty("provider").EnumerateObject().Select(p => p.Name).ToList();
        providerFields.Should().BeEquivalentTo(
            ["displayName", "photoUrl", "rating", "maskedPhone"],
            "no provider id, no email, no status, and no second phone field beside the masked one");
    }

    // ---- Access: 404, never 403 ------------------------------------------

    /// <summary>
    /// Someone else's booking must be indistinguishable from one that does
    /// not exist. 403 would confirm the id is real, which is the leak - so
    /// this asserts the ErrorType is NotFound and, explicitly, that it is not
    /// Forbidden.
    /// </summary>
    [Fact]
    public async Task Another_customers_booking_is_not_found_and_specifically_not_forbidden()
    {
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(Guid.NewGuid(), bookingId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Type.Should().NotBe(ErrorType.Forbidden, "403 would confirm the booking id exists");
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    /// <summary>
    /// The indistinguishability itself, which is the actual security
    /// property: probing a stranger's booking and probing a made-up id must
    /// produce byte-identical answers.
    /// </summary>
    [Fact]
    public async Task Another_customers_booking_answers_exactly_like_a_booking_that_does_not_exist()
    {
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            bookingId = SeedBooking(context, Guid.NewGuid(), BookingStatus.ProviderEnRoute).Id;
        }

        using var readContext = _db.CreateContext();
        var service = Service(readContext);

        var someoneElses = await service.GetForCustomerAsync(Guid.NewGuid(), bookingId);
        var nonExistent = await service.GetForCustomerAsync(Guid.NewGuid(), Guid.NewGuid());

        someoneElses.Error.Type.Should().Be(nonExistent.Error.Type);
        someoneElses.Error.Code.Should().Be(nonExistent.Error.Code);
        someoneElses.Error.Message.Should().Be(nonExistent.Error.Message);
    }

    /// <summary>
    /// The customer's own booking outside the tracking window: still a 404,
    /// not an empty 200 - the tracking sub-resource does not exist once there
    /// is nothing live to watch, and the hub refuses the group for the same
    /// reason. A distinct code from the branch above is safe because a caller
    /// only ever reaches it on a booking they have been proven to own, and it
    /// is what lets the app say "tracking has ended" rather than "no such
    /// booking".
    /// </summary>
    [Theory]
    [InlineData(BookingStatus.AwaitingFulfilment)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.CancelledByCustomer)]
    public async Task A_booking_outside_the_trackable_window_is_not_found_and_specifically_not_forbidden(BookingStatus status)
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            bookingId = SeedBooking(context, customerId, status).Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsFailure.Should().BeTrue("an empty 200 would push the trackable-window rule into every client");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Type.Should().NotBe(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Booking.TrackingUnavailable");
    }

    [Theory]
    [InlineData(BookingStatus.Assigned)]
    [InlineData(BookingStatus.ProviderEnRoute)]
    [InlineData(BookingStatus.InProgress)]
    public async Task Every_trackable_state_is_readable_by_the_owning_customer(BookingStatus status)
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            bookingId = SeedBooking(context, customerId, status).Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsSuccess.Should().BeTrue("trackability comes from BookingLifecycle.IsTrackable, not a local status set");
        result.Value.Status.Should().Be(status);
    }

    // ---- Absent pieces are not errors ------------------------------------

    [Fact]
    public async Task Omits_the_eta_when_none_has_ever_been_computed()
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.Assigned);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsSuccess.Should().BeTrue("no ETA yet is an ordinary state, not a failure");
        result.Value.Eta.Should().BeNull();
        result.Value.ProviderLocation.Should().BeNull("no fix has arrived either");
        result.Value.Provider.Should().NotBeNull("the provider is known even before they start moving");
    }

    /// <summary>
    /// A tracking row exists but its ETA was cleared (task 271 clears on
    /// leaving the trackable states, and a failed route lookup never sets
    /// one). A cleared ETA must read as "no estimate", not as a stale number.
    /// </summary>
    [Fact]
    public async Task Omits_the_eta_when_the_tracking_row_exists_but_carries_none()
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, customerId, BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            context.Add(new BookingTracking(Guid.NewGuid(), booking.Id));
            context.SaveChanges();
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Eta.Should().BeNull();
    }

    [Fact]
    public async Task Omits_the_provider_when_no_live_assignment_stands()
    {
        var customerId = Guid.NewGuid();

        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            bookingId = SeedBooking(context, customerId, BookingStatus.Assigned).Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForCustomerAsync(customerId, bookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().BeNull();
        result.Value.Destination.Should().NotBeNull("the destination is on the booking itself and never depends on a provider");
    }

    // --- Task 284: GetForAdminAsync - no ownership check, otherwise identical rules ---

    [Fact]
    public async Task Admin_reads_a_trackable_booking_regardless_of_which_customer_owns_it()
    {
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.ProviderEnRoute);
            var provider = SeedProvider(context);
            SeedAssignment(context, booking, provider);
            bookingId = booking.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForAdminAsync(bookingId);

        result.IsSuccess.Should().BeTrue("an admin is not scoped to one customer's bookings");
        result.Value.BookingId.Should().Be(bookingId);
        result.Value.Provider.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_read_of_a_nonexistent_booking_is_not_found()
    {
        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForAdminAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task Admin_read_outside_the_trackable_window_is_the_same_no_live_data_code_as_the_customer_path()
    {
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            bookingId = SeedBooking(context, Guid.NewGuid(), BookingStatus.Completed).Id;
        }

        using var readContext = _db.CreateContext();
        var result = await Service(readContext).GetForAdminAsync(bookingId);

        result.IsFailure.Should().BeTrue("a completed booking has no live data left to show - admin-web's ops view renders its own no-live-data state off this");
        result.Error.Code.Should().Be("Booking.TrackingUnavailable");
    }
}
