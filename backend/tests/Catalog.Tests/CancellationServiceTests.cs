using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Cancellations;
using Nestly.Application.Payments;
using Nestly.Application.Pricing;
using Nestly.Application.Refunds;
using Nestly.Application.Serviceability;
using Nestly.Application.Wallet;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 80a-c (eligibility, fee/refund computation, actor+reason capture) and 81 (cancellation API/service).</summary>
public sealed class CancellationServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CancellationServiceTests(TestDatabase db) => _db = db;

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
                TimeProvider.System),
            new PriceCalculationService(
                new ServiceRepository(context),
                new ServiceAddOnRepository(context),
                new ServiceabilityRepository(context),
                new ServiceCityPriceRepository(context),
                new CityPricingPolicyRepository(context)),
            couponService);

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
                TimeProvider.System),
            new NoOpMetricsService());
    }

    private static PaymentWebhookService BuildWebhookService(
        IPaymentTransactionRepository paymentRepository, IBookingRepository bookingRepository,
        Nestly.Infrastructure.Persistence.NestlyDbContext context, IPaymentGateway gateway) =>
        new(
            paymentRepository, bookingRepository, new ServiceRepository(context), gateway,
            new CommissionService(Options.Create(new CommissionOptions())), new EscrowService(new PlatformEscrowLedgerRepository(context)),
            context, new NoOpMetricsService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentWebhookService>.Instance);

    private static CancellationService BuildCancellationService(
        Nestly.Infrastructure.Persistence.NestlyDbContext context, IPaymentGateway gateway, TimeProvider timeProvider, CancellationPolicyOptions? policy = null) =>
        new(
            new BookingRepository(context),
            new PaymentTransactionRepository(context),
            new RefundTransactionRepository(context),
            new RefundService(
                new BookingRepository(context), new PaymentTransactionRepository(context), new RefundTransactionRepository(context),
                new WalletService(new WalletLedgerRepository(context)), new EscrowService(new PlatformEscrowLedgerRepository(context)), gateway, context),
            new BookingCancellationRepository(context),
            timeProvider,
            Options.Create(policy ?? new CancellationPolicyOptions()));

    private sealed record Fixture(Customer Customer, Guid BookingId, decimal Total, DateTime SlotStartUtc);

    /// <summary>A freshly created, fully paid booking (Confirmed) with its slot <paramref name="hoursFromNow"/> away - never cancelled.</summary>
    private async Task<Fixture> SeedPaidBookingAsync(IPaymentGateway gateway, double hoursFromNow, decimal servicePrice = 1000m)
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var pincodeCode = Guid.NewGuid().ToString("N")[..6];
        Customer customer;
        Guid bookingId;
        decimal total;
        TimeSpan slotStart = TimeSpan.FromHours(9);

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
            var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
            var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", servicePrice);
            var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", slotStart, TimeSpan.FromHours(13));
            var rule = new SlotWindowRule(Guid.NewGuid(), window.Id, futureDate.DayOfWeek);

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
            context.SaveChanges();

            var request = new BookingSummaryRequest(service.Id, city.Id, address.Id, locality.Id, window.Id, futureDate, Quantity: 1, []);
            var created = await BuildBookingService(context).CreateAsync(customer.Id, request);
            created.IsSuccess.Should().BeTrue();
            bookingId = created.Value.Id;
            total = created.Value.Price.TotalPayable;
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
        var fakeNow = slotStartUtc.AddHours(-hoursFromNow);
        return new Fixture(customer, bookingId, total, fakeNow);
    }

    [Fact]
    public async Task GetPolicyAsync_reports_full_refund_when_well_outside_the_free_window()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, hoursFromNow: 48, servicePrice: 1001m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc);

        using var context = _db.CreateContext();
        var result = await BuildCancellationService(context, gateway, timeProvider).GetPolicyAsync(fixture.Customer.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeTrue();
        result.Value.WithinFreeCancellationWindow.Should().BeTrue();
        result.Value.CancellationFeeAmount.Should().Be(0m);
        result.Value.RefundAmount.Should().Be(fixture.Total);
    }

    [Fact]
    public async Task CancelAsync_within_the_free_window_fully_refunds_via_the_gateway()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, hoursFromNow: 48, servicePrice: 1003m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc);

        using var context = _db.CreateContext();
        var result = await BuildCancellationService(context, gateway, timeProvider)
            .CancelAsync(fixture.Customer.Id, fixture.BookingId, new CancelBookingRequest("Change of plans"));

        result.IsSuccess.Should().BeTrue();
        result.Value.CancellationFeeAmount.Should().Be(0m);
        result.Value.RefundAmount.Should().Be(fixture.Total);
        result.Value.RefundTransactionId.Should().NotBeNull();
        result.Value.BookingStatus.Should().BeOneOf(BookingStatus.Refunded, BookingStatus.RefundPending);

        using var readContext = _db.CreateContext();
        var record = await new BookingCancellationRepository(readContext).GetByBookingIdAsync(fixture.BookingId);
        record.Should().NotBeNull();
        record!.Actor.Should().Be(CancellationActor.Customer);
        record.Reason.Should().Be("Change of plans");
    }

    [Fact]
    public async Task CancelAsync_inside_the_free_window_charges_a_fee_and_partially_refunds()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, hoursFromNow: 1, servicePrice: 1000m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc);
        var policy = new CancellationPolicyOptions { FreeCancellationWindowHours = 4m, LateCancellationFeePercentage = 20m };

        using var context = _db.CreateContext();
        var result = await BuildCancellationService(context, gateway, timeProvider, policy)
            .CancelAsync(fixture.Customer.Id, fixture.BookingId, new CancelBookingRequest("Too late but trying anyway"));

        result.IsSuccess.Should().BeTrue();
        result.Value.WithinFreeCancellationWindow.Should().BeFalse();
        result.Value.CancellationFeeAmount.Should().Be(200m);
        result.Value.RefundAmount.Should().Be(800m);
    }

    [Fact]
    public async Task CancelAsync_rejects_a_booking_already_in_a_terminal_status()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, hoursFromNow: 48, servicePrice: 1000m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc);
        var service = BuildCancellationService(_db.CreateContext(), gateway, timeProvider);

        // First cancellation succeeds and moves the booking out of the cancellable set.
        var first = await service.CancelAsync(fixture.Customer.Id, fixture.BookingId, new CancelBookingRequest("First cancel"));
        first.IsSuccess.Should().BeTrue();

        var second = await service.CancelAsync(fixture.Customer.Id, fixture.BookingId, new CancelBookingRequest("Second cancel"));
        second.IsSuccess.Should().BeFalse();
        second.Error.Code.Should().Be("Cancellation.NotEligible");
    }

    [Fact]
    public async Task CancelAsync_rejects_another_customers_booking()
    {
        var gateway = BuildGateway();
        var fixture = await SeedPaidBookingAsync(gateway, hoursFromNow: 48, servicePrice: 1000m);
        var timeProvider = new FakeTimeProvider(fixture.SlotStartUtc);

        using var context = _db.CreateContext();
        var result = await BuildCancellationService(context, gateway, timeProvider)
            .CancelAsync(Guid.NewGuid(), fixture.BookingId, new CancelBookingRequest("Not my booking"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cancellation.BookingNotFound");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime now) => _now = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
