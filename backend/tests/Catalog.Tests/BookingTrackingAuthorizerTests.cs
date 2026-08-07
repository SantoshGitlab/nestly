using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Realtime;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 273 fix (c): the three-sided access rule for the tracking hub - the
/// owning customer, the provider on the LIVE assignment, and an admin holding
/// the bookings-read permission - and nobody else, including the default case.
///
/// Runs against the real repositories on SQLite (see <see cref="TestDatabase"/>);
/// the only stand-in is the <see cref="RealtimeActorContext"/>, which in
/// production is fixed per API process by whichever JWT scheme that process
/// registered. Varying it here is exactly what lets one test assert what
/// consumer-api would decide and the next what provider-api would, without
/// booting either.
/// </summary>
public sealed class BookingTrackingAuthorizerTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingTrackingAuthorizerTests(TestDatabase db) => _db = db;

    private static BookingTrackingAuthorizer Authorizer(NestlyDbContext context, RealtimeActorKind kind) =>
        new(new RealtimeActorContext(kind),
            new BookingRepository(context),
            new BookingProviderAssignmentRepository(context));

    private static ClaimsPrincipal Principal(Guid subjectId, params Claim[] additionalClaims)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subjectId.ToString()) };
        claims.AddRange(additionalClaims);

        // A non-null authentication type is what makes IsAuthenticated true -
        // the same thing the JWT bearer handler produces on a real handshake.
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestJwt"));
    }

    private static Claim AdminPermission(string code) => new(AdminClaimTypes.Permission, code);

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

    /// <summary>The legal transition chain from a freshly created booking to <paramref name="status"/> (BookingLifecycle is the authority; a test may not shortcut it).</summary>
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

    private static Provider SeedProvider(NestlyDbContext context)
    {
        var provider = new Provider(
            Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual,
            "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(provider);
        context.SaveChanges();
        return provider;
    }

    private static BookingProviderAssignment SeedAssignment(
        NestlyDbContext context, Booking booking, Provider provider, bool accepted = true)
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), booking.Id, provider.Id, BookingAssignedByType.System, null, null);
        if (accepted)
        {
            assignment.Accept();
        }

        context.Add(assignment);
        context.SaveChanges();
        return assignment;
    }

    // ---- Side 1: the owning customer -------------------------------------

    [Fact]
    public async Task Allows_the_owning_customer_while_the_booking_is_trackable()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, BookingStatus.Assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(Principal(customerId), booking.Id);

        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(BookingStatus.Assigned)]
    [InlineData(BookingStatus.ProviderEnRoute)]
    [InlineData(BookingStatus.InProgress)]
    public async Task Allows_the_owning_customer_in_every_trackable_state(BookingStatus status)
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, status);

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(Principal(customerId), booking.Id);

        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(BookingStatus.AwaitingFulfilment)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.CancelledByCustomer)]
    public async Task Denies_the_owning_customer_once_the_booking_is_no_longer_trackable(BookingStatus status)
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, status);

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(Principal(customerId), booking.Id);

        allowed.Should().BeFalse("tracking is a live view of fulfilment, not a permanent right over one's own booking");
    }

    [Fact]
    public async Task Denies_a_customer_who_does_not_own_the_booking()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(Principal(Guid.NewGuid()), booking.Id);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_a_booking_that_does_not_exist_exactly_like_one_that_is_not_yours()
    {
        using var context = _db.CreateContext();

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(Principal(Guid.NewGuid()), Guid.NewGuid());

        allowed.Should().BeFalse();
    }

    // ---- Side 2: the provider on the live assignment ----------------------

    [Fact]
    public async Task Allows_the_provider_on_the_live_assignment()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);
        var provider = SeedProvider(context);
        SeedAssignment(context, booking, provider);

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(provider.Id), booking.Id);

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_a_provider_whose_assignment_is_no_longer_live()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);
        var provider = SeedProvider(context);
        var assignment = SeedAssignment(context, booking, provider, accepted: false);

        assignment.Reject("busy");
        context.SaveChanges();

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(provider.Id), booking.Id);

        allowed.Should().BeFalse("a rejected assignment keeps its history row but loses the live feed with it");
    }

    [Fact]
    public async Task Denies_a_provider_who_was_never_assigned_this_booking()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);
        var assigned = SeedProvider(context);
        var stranger = SeedProvider(context);
        SeedAssignment(context, booking, assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(stranger.Id), booking.Id);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_the_assigned_provider_once_the_booking_is_no_longer_trackable()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Completed);
        var provider = SeedProvider(context);
        SeedAssignment(context, booking, provider);

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(provider.Id), booking.Id);

        allowed.Should().BeFalse("the assignment stays Accepted after completion, so the fulfilment window is what closes the feed");
    }

    [Fact]
    public async Task Denies_the_assigned_provider_before_they_have_accepted_the_job()
    {
        using var context = _db.CreateContext();
        // An outstanding offer leaves the booking in AwaitingFulfilment - no
        // location is collected before accept (task 269), so there is nothing
        // to watch either.
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.AwaitingFulfilment);
        var provider = SeedProvider(context);
        SeedAssignment(context, booking, provider, accepted: false);

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(provider.Id), booking.Id);

        allowed.Should().BeFalse();
    }

    // ---- Side 3: the admin ------------------------------------------------

    [Fact]
    public async Task Allows_an_admin_holding_the_bookings_read_permission()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Admin).CanTrackAsync(
            Principal(Guid.NewGuid(), AdminPermission(BookingTrackingAuthorizer.AdminPermissionCode)),
            booking.Id);

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_an_admin_without_that_permission_even_holding_others()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Admin).CanTrackAsync(
            Principal(Guid.NewGuid(),
                AdminPermission(AdminPermissionCatalog.BuildCode(AdminModules.Support, AdminPermissionAction.Read)),
                AdminPermission(AdminPermissionCatalog.BuildCode(AdminModules.Catalog, AdminPermissionAction.Write))),
            booking.Id);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void The_admin_permission_checked_is_a_real_grantable_code()
    {
        // A typo here would deny every admin forever, silently - a
        // fail-closed check cannot fail loudly.
        AdminPermissionCatalog.Permissions.Select(p => p.Code)
            .Should().Contain(BookingTrackingAuthorizer.AdminPermissionCode);
    }

    // ---- The fail-closed default -----------------------------------------

    [Fact]
    public async Task Denies_everyone_when_the_process_declares_no_actor_kind()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, BookingStatus.Assigned);

        // Same principal that is allowed above under RealtimeActorKind.Customer.
        var allowed = await Authorizer(context, RealtimeActorKind.Unknown)
            .CanTrackAsync(Principal(customerId), booking.Id);

        allowed.Should().BeFalse("an unconfigured process must authorize nobody rather than everybody");
    }

    [Fact]
    public async Task Denies_an_unauthenticated_principal()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, BookingStatus.Assigned);

        // No authentication type - ClaimsIdentity.IsAuthenticated is false
        // even though the sub claim is the owner's.
        var anonymous = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString())]));

        var allowed = await Authorizer(context, RealtimeActorKind.Customer)
            .CanTrackAsync(anonymous, booking.Id);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_a_null_principal()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);

        var allowed = await Authorizer(context, RealtimeActorKind.Customer).CanTrackAsync(null, booking.Id);

        allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(RealtimeActorKind.Customer)]
    [InlineData(RealtimeActorKind.Provider)]
    public async Task Denies_an_authenticated_token_carrying_no_usable_subject(RealtimeActorKind kind)
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);
        var noSubject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("mobile", "9000000000")], "TestJwt"));

        var allowed = await Authorizer(context, kind).CanTrackAsync(noSubject, booking.Id);

        allowed.Should().BeFalse();
    }

    // ---- The sides do not bleed into one another --------------------------

    [Fact]
    public async Task Does_not_treat_a_providers_subject_id_as_a_customers_on_the_customer_side()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);
        var provider = SeedProvider(context);
        SeedAssignment(context, booking, provider);

        var authorizer = Authorizer(context, RealtimeActorKind.Customer);

        // Customer and provider tokens carry an identical claim set, so the
        // only thing separating them is the actor kind of the process that
        // validated the token. On consumer-api this id owns no booking.
        (await authorizer.CanTrackAsync(Principal(provider.Id), booking.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_treat_a_customers_subject_id_as_a_providers_on_the_provider_side()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId, BookingStatus.Assigned);
        SeedAssignment(context, booking, SeedProvider(context));

        var allowed = await Authorizer(context, RealtimeActorKind.Provider)
            .CanTrackAsync(Principal(customerId), booking.Id);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_honour_a_permission_claim_outside_the_admin_process()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid(), BookingStatus.Assigned);

        // A self-minted "permission" claim on a customer token buys nothing:
        // the admin branch is only reachable in a process whose scheme is the
        // admin one, and admin tokens are signed with a different key.
        var allowed = await Authorizer(context, RealtimeActorKind.Customer).CanTrackAsync(
            Principal(Guid.NewGuid(), AdminPermission(BookingTrackingAuthorizer.AdminPermissionCode)),
            booking.Id);

        allowed.Should().BeFalse();
    }
}
