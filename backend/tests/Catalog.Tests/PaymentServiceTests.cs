using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.Application.Pricing;
using Nestly.Application.Serviceability;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 68a-d: gateway order creation, idempotent dedup, and the retry entry point (task 70).</summary>
public sealed class PaymentServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public PaymentServiceTests(TestDatabase db) => _db = db;

    private static SandboxPaymentGateway BuildGateway() =>
        new(Options.Create(new SandboxGatewayOptions { WebhookSigningSecret = "unit-test-signing-secret-value" }));

    private static BookingService BuildBookingService(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
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
                TimeProvider.System),
            new PriceCalculationService(
                new ServiceRepository(context),
                new ServiceAddOnRepository(context),
                new ServiceabilityRepository(context),
                new ServiceCityPriceRepository(context),
                new CityPricingPolicyRepository(context)));

        return new BookingService(summaryService, new BookingRepository(context), new CustomerRepository(context));
    }

    private static PaymentService BuildPaymentService(Nestly.Infrastructure.Persistence.NestlyDbContext context, IPaymentGateway gateway) =>
        new(new PaymentTransactionRepository(context), new BookingRepository(context), gateway);

    private sealed record Fixture(Customer Customer, Guid BookingId, decimal Total);

    private async Task<Fixture> SeedBookingAsync(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var pincodeCode = Guid.NewGuid().ToString("N")[..6];
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var address = new CustomerAddress(
            Guid.NewGuid(), customer.Id, "Home", "221B Baker Street", null, null,
            pincodeCode, "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210", true);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, pincodeCode);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Koramangala");
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));
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

        return new Fixture(customer, created.Value.Id, created.Value.Price.TotalPayable);
    }

    [Fact]
    public async Task CreateOrderAsync_starts_the_first_attempt_on_a_new_transaction()
    {
        Fixture fixture;
        using (var seedContext = _db.CreateContext())
        {
            fixture = await SeedBookingAsync(seedContext);
        }

        using var context = _db.CreateContext();
        var result = await BuildPaymentService(context, BuildGateway()).CreateOrderAsync(
            fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(fixture.Total);
        result.Value.AttemptNumber.Should().Be(1);
        result.Value.GatewayOrderId.Should().StartWith("sandbox_order_");

        using var readContext = _db.CreateContext();
        var transaction = await new PaymentTransactionRepository(readContext).GetByBookingIdAsync(fixture.BookingId);
        transaction.Should().NotBeNull();
        transaction!.Status.Should().Be(PaymentTransactionStatus.Pending);
        transaction.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrderAsync_is_idempotent_while_an_attempt_is_in_flight()
    {
        Fixture fixture;
        using (var seedContext = _db.CreateContext())
        {
            fixture = await SeedBookingAsync(seedContext);
        }

        var gateway = BuildGateway();

        using (var firstContext = _db.CreateContext())
        {
            var first = await BuildPaymentService(firstContext, gateway).CreateOrderAsync(
                fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));
            first.IsSuccess.Should().BeTrue();
        }

        using var secondContext = _db.CreateContext();
        var second = await BuildPaymentService(secondContext, gateway).CreateOrderAsync(
            fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));

        second.IsSuccess.Should().BeTrue();
        second.Value.AttemptNumber.Should().Be(1);

        using var readContext = _db.CreateContext();
        var transaction = await new PaymentTransactionRepository(readContext).GetByBookingIdAsync(fixture.BookingId);
        // Still exactly one attempt - the duplicate request did not mint a second gateway order.
        transaction!.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrderAsync_retries_on_the_same_transaction_after_a_failed_attempt()
    {
        Fixture fixture;
        using (var seedContext = _db.CreateContext())
        {
            fixture = await SeedBookingAsync(seedContext);
        }

        var gateway = BuildGateway();
        Guid transactionId;

        using (var firstContext = _db.CreateContext())
        {
            var first = await BuildPaymentService(firstContext, gateway).CreateOrderAsync(
                fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));
            transactionId = first.Value.PaymentTransactionId;
        }

        using (var failContext = _db.CreateContext())
        {
            var repository = new PaymentTransactionRepository(failContext);
            var transaction = await repository.GetByIdAsync(transactionId);
            transaction!.MarkAttemptFailed(transaction.Attempts[0].Id, "Sandbox declined.");
            await repository.UpdateAsync(transaction);

            var booking = await new BookingRepository(failContext).GetByIdAsync(fixture.BookingId);
            booking!.TransitionTo(BookingStatus.PaymentFailed, "Sandbox declined.");
            await new BookingRepository(failContext).UpdateAsync(booking);
        }

        using var retryContext = _db.CreateContext();
        var retry = await BuildPaymentService(retryContext, gateway).CreateOrderAsync(
            fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));

        retry.IsSuccess.Should().BeTrue();
        retry.Value.PaymentTransactionId.Should().Be(transactionId);
        retry.Value.AttemptNumber.Should().Be(2);

        using var readContext = _db.CreateContext();
        var reloaded = await new PaymentTransactionRepository(readContext).GetByIdAsync(transactionId);
        reloaded!.Attempts.Should().HaveCount(2);
        reloaded.Status.Should().Be(PaymentTransactionStatus.Pending);
    }

    [Fact]
    public async Task CreateOrderAsync_rejects_a_booking_that_is_not_awaiting_payment()
    {
        Fixture fixture;
        using (var seedContext = _db.CreateContext())
        {
            fixture = await SeedBookingAsync(seedContext);
        }

        using (var confirmContext = _db.CreateContext())
        {
            var repository = new BookingRepository(confirmContext);
            var booking = await repository.GetByIdAsync(fixture.BookingId);
            booking!.TransitionTo(BookingStatus.Confirmed, "test setup");
            await repository.UpdateAsync(booking);
        }

        using var context = _db.CreateContext();
        var result = await BuildPaymentService(context, BuildGateway()).CreateOrderAsync(
            fixture.Customer.Id, new CreatePaymentOrderRequest(fixture.BookingId, IdempotencyKey: null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.BookingNotPayable");
    }
}
