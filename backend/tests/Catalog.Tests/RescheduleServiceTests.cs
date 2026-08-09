using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.Application.Pricing;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Reschedules;
using Nestly.Application.Serviceability;
using Nestly.Application.Slots;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 82a-d (eligibility window, count limits, slot revalidation, fee impact) and 83 (reschedule API/service).</summary>
public sealed class RescheduleServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public RescheduleServiceTests(TestDatabase db) => _db = db;

    private static SandboxPaymentGateway BuildGateway() =>
        new(Options.Create(new SandboxGatewayOptions { WebhookSigningSecret = "unit-test-signing-secret-value" }));

    private static BookingService BuildBookingService(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var couponService = new CouponService(new CouponRepository(context), new CouponRedemptionRepository(context), new BookingRepository(context), TimeProvider.System);
        var summaryService = new BookingSummaryService(
            new ServiceRepository(context),
            new ServiceAddOnRepository(context),
            new CustomerAddressRepository(context),
            new SlotAvailabilityService(
                new ServiceabilityRepository(context),
                new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService()),
                new SlotWindowRepository(context),
                new SlotBlackoutRepository(context),
                new SlotBookingPolicyRepository(context),
                new SlotCapacityRepository(context),
                TestServices.Clock()),
            new PriceCalculationService(
                new ServiceRepository(context),
                new ServiceAddOnRepository(context),
                new ServiceabilityRepository(context),
                new ServiceCityPriceRepository(context),
                new CityPricingPolicyRepository(context)),
            couponService,
            new SubscriptionBenefitService(new CustomerSubscriptionRepository(context)),
            new WalletService(new WalletLedgerRepository(context), context),
        new ServiceabilityRepository(context),
        TestServices.BookingOptions());

        return new BookingService(
            summaryService,
            new BookingRepository(context),
            new CustomerRepository(context),
            couponService,
            new SlotAvailabilityService(
                new ServiceabilityRepository(context),
                new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService()),
                new SlotWindowRepository(context),
                new SlotBlackoutRepository(context),
                new SlotBookingPolicyRepository(context),
                new SlotCapacityRepository(context),
                TestServices.Clock()),
            new NoOpMetricsService(),
            new BookingProviderAssignmentRepository(context),
            new ProviderRepository(context),
            new ReviewRepository(context),
            new CustomerSubscriptionRepository(context),
            new WalletService(new WalletLedgerRepository(context), context),
            context);
    }

    private static PaymentWebhookService BuildWebhookService(
        IPaymentTransactionRepository paymentRepository, IBookingRepository bookingRepository,
        Nestly.Infrastructure.Persistence.NestlyDbContext context, IPaymentGateway gateway) =>
        new(
            paymentRepository, bookingRepository, new ServiceRepository(context), gateway,
            new CommissionService(Options.Create(new CommissionOptions())), new EscrowService(new PlatformEscrowLedgerRepository(context)),
            context, new NoOpMetricsService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentWebhookService>.Instance);

    private static ISlotAvailabilityService BuildSlotAvailabilityService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new SlotAvailabilityService(
            new ServiceabilityRepository(context),
            new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService()),
            new SlotWindowRepository(context),
            new SlotBlackoutRepository(context),
            new SlotBookingPolicyRepository(context),
            new SlotCapacityRepository(context),
            TestServices.Clock());

    private static RescheduleService BuildRescheduleService(
        Nestly.Infrastructure.Persistence.NestlyDbContext context, TimeProvider timeProvider, ReschedulePolicyOptions? policy = null) =>
        new(
            new BookingRepository(context),
            new PaymentTransactionRepository(context),
            new RefundTransactionRepository(context),
            BuildSlotAvailabilityService(context),
            new BookingRescheduleRepository(context),
            new BookingProviderAssignmentRepository(context),
            new ProviderScheduleConflictService(context),
            context,
            TestServices.Clock(timeProvider),
            timeProvider,
            Options.Create(policy ?? new ReschedulePolicyOptions()));

    private sealed record Fixture(Customer Customer, Guid BookingId, decimal Total, Guid LocalityId, Guid NewSlotWindowId, DateOnly NewSlotDate, DateTime SlotStartUtc);

    /// <summary>A freshly created, fully paid booking (Confirmed) with its slot far in the future, plus a second slot window available on a later date to reschedule into.</summary>
    private async Task<Fixture> SeedPaidBookingAsync(IPaymentGateway gateway, decimal servicePrice = 1000m)
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var newDate = futureDate.AddDays(2);
        var pincodeCode = Guid.NewGuid().ToString("N")[..6];
        Customer customer;
        Guid bookingId, localityId, newWindowId;
        decimal total;
        var slotStart = TimeSpan.FromHours(9);

        using (var context = _db.CreateContext())
        {
            customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
            var address = new CustomerAddress(
                Guid.NewGuid(), customer.Id, "Home", "221B Baker Street", null, null,
                pincodeCode, "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210", true);
            var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
            var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
            var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
            var pincode = new Pincode(Guid.NewGuid(), city.Id, pincodeCode);
            var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Koramangala");
            address.LinkToGeography(pincode.Id, locality.Id);
            var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
            var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", servicePrice);
            var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", slotStart, TimeSpan.FromHours(13));
            var rule = new SlotWindowRule(Guid.NewGuid(), window.Id, futureDate.DayOfWeek);
            var newWindow = new SlotWindow(Guid.NewGuid(), city.Id, "Afternoon", TimeSpan.FromHours(14), TimeSpan.FromHours(18));
            var newRule = new SlotWindowRule(Guid.NewGuid(), newWindow.Id, newDate.DayOfWeek);

            context.Add(customer);
            context.Add(address);
            context.States.Add(state);
            context.Cities.Add(city);
            context.Zones.Add(zone);
            context.Pincodes.Add(pincode);
            context.Localities.Add(locality);
            context.Add(category);
            context.Add(service);
            context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), service.Id, pincode.Id));
            context.SlotWindows.Add(window);
            context.SlotWindowRules.Add(rule);
            context.SlotWindows.Add(newWindow);
            context.SlotWindowRules.Add(newRule);
            context.SaveChanges();

            var request = new BookingSummaryRequest(service.Id, city.Id, address.Id, locality.Id, window.Id, futureDate, Quantity: 1, []);
            var created = await BuildBookingService(context).CreateAsync(customer.Id, request);
            created.IsSuccess.Should().BeTrue();
            bookingId = created.Value.Id;
            total = created.Value.Price.TotalPayable;
            localityId = locality.Id;
            newWindowId = newWindow.Id;
        }

        string gatewayOrderId;
        using (var orderContext = _db.CreateContext())
        {
            var paymentRepository = new PaymentTransactionRepository(orderContext);
            var bookingRepository = new BookingRepository(orderContext);
            var paymentService = new PaymentService(
                paymentRepository, bookingRepository, gateway, (ISandboxPaymentSimulator)gateway,
                BuildWebhookService(paymentRepository, bookingRepository, orderContext, gateway));
            var order = await paymentService.CreateOrderAsync(customer.Id, new CreatePaymentOrderRequest(bookingId, null));
            gatewayOrderId = order.Value.GatewayOrderId;
        }

        using (var callbackContext = _db.CreateContext())
        {
            var paymentRepository = new PaymentTransactionRepository(callbackContext);
            var bookingRepository = new BookingRepository(callbackContext);
            var webhookService = BuildWebhookService(paymentRepository, bookingRepository, callbackContext, gateway);
            string payload = PaymentWebhookPayload.Build(gatewayOrderId, "sandbox_pay_ref", PaymentWebhookPayload.SuccessStatus);
            string signature = gateway.SignPayload(payload);
            var callback = await webhookService.HandleCallbackAsync(new PaymentWebhookRequest(gatewayOrderId, "sandbox_pay_ref", PaymentWebhookPayload.SuccessStatus, signature));
            callback.IsSuccess.Should().BeTrue();
        }

        var slotStartUtc = futureDate.ToDateTime(TimeOnly.MinValue).Add(slotStart);
        return new Fixture(customer, bookingId, total, localityId, newWindowId, newDate, slotStartUtc);
    }

    [Fact]
    public async Task GetEligibilityAsync_is_eligible_well_before_the_slot()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 1001m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));

        using var context = _db.CreateContext();
        var result = await BuildRescheduleService(context, timeProvider).GetEligibilityAsync(fixture.Customer.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeTrue();
        result.Value.ReschedulesUsed.Should().Be(0);
    }

    [Fact]
    public async Task GetEligibilityAsync_blocks_reschedule_once_the_window_has_expired()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 1002m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddHours(-1)); // policy default MinHoursBeforeSlot = 2

        using var context = _db.CreateContext();
        var result = await BuildRescheduleService(context, timeProvider).GetEligibilityAsync(fixture.Customer.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeFalse();
        result.Value.IneligibilityReason.Should().Contain("expired");
    }

    [Fact]
    public async Task ConfirmRescheduleAsync_updates_the_booking_slot_and_records_history()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 1003m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));

        using var context = _db.CreateContext();
        var result = await BuildRescheduleService(context, timeProvider).ConfirmRescheduleAsync(
            fixture.Customer.Id, fixture.BookingId, new RescheduleBookingRequest(fixture.LocalityId, fixture.NewSlotWindowId, fixture.NewSlotDate, "Need a different day"));

        result.IsSuccess.Should().BeTrue();
        result.Value.NewSlot.SlotWindowId.Should().Be(fixture.NewSlotWindowId);
        result.Value.NewSlot.Date.Should().Be(fixture.NewSlotDate);
        result.Value.IsLate.Should().BeFalse();
        result.Value.FeeAmount.Should().Be(0m);
        result.Value.ReschedulesUsed.Should().Be(1);
        result.Value.BookingStatus.Should().Be(BookingStatus.AwaitingFulfilment);

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(fixture.BookingId);
        booking!.SlotWindowId.Should().Be(fixture.NewSlotWindowId);
        booking.SlotDate.Should().Be(fixture.NewSlotDate);
        booking.StatusHistory.Should().Contain(h => h.ToStatus == BookingStatus.Rescheduled);

        var history = await new BookingRescheduleRepository(readContext).ListByBookingAsync(fixture.BookingId);
        history.Should().HaveCount(1);
        history[0].Reason.Should().Be("Need a different day");
    }

    [Fact]
    public async Task ConfirmRescheduleAsync_rejects_a_slot_window_that_does_not_exist_on_the_requested_date()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 1004m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));

        using var context = _db.CreateContext();
        var result = await BuildRescheduleService(context, timeProvider).ConfirmRescheduleAsync(
            fixture.Customer.Id, fixture.BookingId, new RescheduleBookingRequest(fixture.LocalityId, Guid.NewGuid(), fixture.NewSlotDate, null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Reschedule.SlotNotAvailable");
    }

    [Fact]
    public async Task ConfirmRescheduleAsync_stops_once_the_reschedule_count_limit_is_reached()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 1005m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));
        var policy = new ReschedulePolicyOptions { MaxReschedulesPerBooking = 1 };

        using (var firstContext = _db.CreateContext())
        {
            var first = await BuildRescheduleService(firstContext, timeProvider, policy).ConfirmRescheduleAsync(
                fixture.Customer.Id, fixture.BookingId, new RescheduleBookingRequest(fixture.LocalityId, fixture.NewSlotWindowId, fixture.NewSlotDate, "First reschedule"));
            first.IsSuccess.Should().BeTrue();
        }

        using var context = _db.CreateContext();
        var result = await BuildRescheduleService(context, timeProvider, policy).GetEligibilityAsync(fixture.Customer.Id, fixture.BookingId);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.IneligibilityReason.Should().Contain("maximum");
    }

    // --- Task 290: rescheduling an Assigned booking must not silently keep
    // (or silently drop) the provider - it must check the new slot. ---

    private static BookingProviderAssignmentService BuildAssignmentService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new BookingRepository(context), new ProviderRepository(context), new ServiceRepository(context),
        new BookingProviderAssignmentRepository(context), new ProviderScheduleConflictService(context), context);

    private static Provider SeedProvider(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var provider = new Provider(
            Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual,
            "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(provider);
        context.SaveChanges();
        return provider;
    }

    /// <summary>Walks the booking to AwaitingFulfilment and assigns <paramref name="providerId"/> via the real assignment service, exactly the way task 147's admin flow does.</summary>
    private static async Task AssignProviderAsync(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid bookingId, Guid providerId)
    {
        var booking = await new BookingRepository(context).GetByIdAsync(bookingId);
        booking!.TransitionTo(BookingStatus.AwaitingFulfilment, "test");
        await new BookingRepository(context).UpdateAsync(booking);

        var result = await BuildAssignmentService(context).AssignAsync(
            bookingId, Guid.NewGuid(), new AssignProviderRequest(providerId, ResponseDeadline: null));
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>A second, minimal booking occupying the given provider's entire slot window on <paramref name="date"/> - the conflict test's collision.</summary>
    private static void SeedConflictingAssignment(
        Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid providerId, DateOnly date, TimeSpan startTime, TimeSpan endTime)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Other Customer", CustomerStatus.Active);
        context.Add(customer);

        var booking = new Booking(
            Guid.NewGuid(), customer.Id,
            new CustomerSnapshot("Other Customer", customer.Mobile),
            null,
            new AddressSnapshot("Home", "1 Other St", null, null, "560002", "Bengaluru", "Karnataka", 12.95m, 77.6m, "Other", "9000000001"),
            new SlotSnapshot(Guid.NewGuid(), date, "Conflict window", startTime, endTime),
            new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));
        foreach (var step in new[] { BookingStatus.PaymentPending, BookingStatus.Confirmed, BookingStatus.AwaitingFulfilment, BookingStatus.Assigned })
        {
            booking.TransitionTo(step, "test");
        }

        context.Add(booking);
        context.SaveChanges();

        var assignment = new BookingProviderAssignment(Guid.NewGuid(), booking.Id, providerId, BookingAssignedByType.System, null, null);
        assignment.Accept();
        context.Add(assignment);
        context.SaveChanges();
    }

    [Fact]
    public async Task ConfirmRescheduleAsync_keeps_the_assigned_provider_when_the_new_slot_is_still_free_for_them()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 2001m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));

        Guid providerId;
        using (var context = _db.CreateContext())
        {
            providerId = SeedProvider(context).Id;
            await AssignProviderAsync(context, fixture.BookingId, providerId);
        }

        using (var context = _db.CreateContext())
        {
            var result = await BuildRescheduleService(context, timeProvider).ConfirmRescheduleAsync(
                fixture.Customer.Id, fixture.BookingId,
                new RescheduleBookingRequest(fixture.LocalityId, fixture.NewSlotWindowId, fixture.NewSlotDate, "Need a different day"));

            result.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(fixture.BookingId);

        booking!.Status.Should().Be(BookingStatus.Assigned, "the provider was free at the new slot, so the reschedule kept them assigned rather than falling back to AwaitingFulfilment");
        booking.AssignedProviderId.Should().Be(providerId);

        var activeAssignment = await new BookingProviderAssignmentRepository(readContext).GetActiveByBookingAsync(fixture.BookingId);
        activeAssignment.Should().NotBeNull();
        activeAssignment!.ProviderId.Should().Be(providerId);
        activeAssignment.Status.Should().Be(BookingProviderAssignmentStatus.Assigned, "the original assignment row survives untouched when the reschedule keeps the same provider");
    }

    /// <summary>
    /// The core of task 290: before this fix, Booking.Reschedule moved the
    /// slot while leaving AssignedProviderId and the live assignment row
    /// untouched, with nothing checking whether the provider was even free
    /// at the new time - so a reschedule could silently slide a booking on
    /// top of the same provider's other job.
    /// </summary>
    [Fact]
    public async Task ConfirmRescheduleAsync_drops_the_assigned_provider_when_the_new_slot_now_conflicts_with_another_job()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, 2002m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc.AddDays(-5));

        Guid providerId;
        using (var context = _db.CreateContext())
        {
            providerId = SeedProvider(context).Id;
            await AssignProviderAsync(context, fixture.BookingId, providerId);

            // Occupies the provider's entire day on the target reschedule
            // date (the new slot window is 14:00-18:00, see SeedPaidBookingAsync) -
            // any reschedule into that window now collides.
            SeedConflictingAssignment(context, providerId, fixture.NewSlotDate, TimeSpan.FromHours(0), TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        }

        using (var context = _db.CreateContext())
        {
            var result = await BuildRescheduleService(context, timeProvider).ConfirmRescheduleAsync(
                fixture.Customer.Id, fixture.BookingId,
                new RescheduleBookingRequest(fixture.LocalityId, fixture.NewSlotWindowId, fixture.NewSlotDate, "Need a different day"));

            result.IsSuccess.Should().BeTrue("the slot move itself must still succeed even though the provider has to be dropped");
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(fixture.BookingId);

        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment, "the provider now conflicts with another job, so the booking needs reassignment rather than silently double-booking them");
        booking.AssignedProviderId.Should().BeNull();

        var activeAssignment = await new BookingProviderAssignmentRepository(readContext).GetActiveByBookingAsync(fixture.BookingId);
        activeAssignment.Should().BeNull("the original assignment was withdrawn, not left live alongside a cleared display field");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime now) => _now = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
