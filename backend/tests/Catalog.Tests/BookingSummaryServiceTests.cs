using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Pricing;
using Nestly.Application.Serviceability;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 57: booking summary service, composed from the already-tested slot/price services.</summary>
public sealed class BookingSummaryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingSummaryServiceTests(TestDatabase db) => _db = db;

    private BookingSummaryService BuildService(
        Nestly.Infrastructure.Persistence.NestlyDbContext context,
        Microsoft.Extensions.Options.IOptions<Nestly.Infrastructure.Options.BookingOptions>? bookingOptions = null) => new(
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
        new CouponService(
            new CouponRepository(context),
            new CouponRedemptionRepository(context),
            new BookingRepository(context),
            TimeProvider.System),
        new SubscriptionBenefitService(new CustomerSubscriptionRepository(context)),
        new ServiceabilityRepository(context),
        bookingOptions ?? TestServices.BookingOptions());

    private sealed record Fixture(
        Customer Customer, CustomerAddress Address, State State, City City, Pincode Pincode,
        Locality Locality, Category Category, Service Service, ServiceAddOn AddOn, SlotWindow Window, DateOnly Date);

    private Fixture Seed(Nestly.Infrastructure.Persistence.NestlyDbContext context)
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
        address.LinkToGeography(pincode.Id, locality.Id);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 150m);
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
        context.Add(addOn);
        context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), service.Id, pincode.Id));
        context.SlotWindows.Add(window);
        context.SlotWindowRules.Add(rule);
        context.SaveChanges();

        return new Fixture(customer, address, state, city, pincode, locality, category, service, addOn, window, futureDate);
    }

    private static BookingSummaryRequest RequestFor(Fixture f, IReadOnlyList<AddOnSelection>? addOns = null) => new(
        f.Service.Id, f.City.Id, f.Address.Id, f.Locality.Id, f.Window.Id, f.Date, Quantity: 1, addOns ?? []);

    [Fact]
    public async Task Returns_a_full_summary_for_a_valid_request()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetSummaryAsync(
            fixture.Customer.Id, RequestFor(fixture, [new AddOnSelection(fixture.AddOn.Id, 1)]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Service.Id.Should().Be(fixture.Service.Id);
        result.Value.AddOns.Should().ContainSingle(a => a.Id == fixture.AddOn.Id);
        result.Value.Address.Id.Should().Be(fixture.Address.Id);
        result.Value.Slot.SlotWindowId.Should().Be(fixture.Window.Id);
        result.Value.Price.TotalPayable.Should().Be(650m);
    }

    [Fact]
    public async Task Unknown_service_returns_not_found()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
        }

        using var readContext = _db.CreateContext();
        var request = RequestFor(fixture) with { ServiceId = Guid.NewGuid() };
        var result = await BuildService(readContext).GetSummaryAsync(fixture.Customer.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.ServiceNotFound");
    }

    [Fact]
    public async Task An_address_belonging_to_a_different_customer_returns_not_found()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetSummaryAsync(Guid.NewGuid(), RequestFor(fixture));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.AddressNotFound");
    }

    [Fact]
    public async Task An_addon_that_does_not_belong_to_the_service_is_rejected()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
        }

        using var readContext = _db.CreateContext();
        var request = RequestFor(fixture, [new AddOnSelection(Guid.NewGuid(), 1)]);
        var result = await BuildService(readContext).GetSummaryAsync(fixture.Customer.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.InvalidAddOn");
    }

    [Fact]
    public async Task A_locality_with_no_pincode_mapping_to_the_service_is_reported_not_serviceable()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
            // A second locality/pincode the service was never mapped to. The
            // customer's address has to move there too: booking a locality the
            // address does not belong to is now its own rejection
            // (Booking.AddressOutsideSelectedArea, covered below), and it
            // would mask the serviceability rule this test is about.
            var otherPincode = new Pincode(Guid.NewGuid(), fixture.City.Id, Guid.NewGuid().ToString("N")[..6]);
            var otherZone = new Zone(Guid.NewGuid(), fixture.City.Id, "Other");
            var otherLocality = new Locality(Guid.NewGuid(), otherZone.Id, otherPincode.Id, "Whitefield");
            context.Pincodes.Add(otherPincode);
            context.Zones.Add(otherZone);
            context.Localities.Add(otherLocality);

            var address = context.Set<CustomerAddress>().Single(a => a.Id == fixture.Address.Id);
            address.LinkToGeography(otherPincode.Id, otherLocality.Id);
            context.SaveChanges();

            var request = RequestFor(fixture) with { LocalityId = otherLocality.Id };
            var result = await BuildService(context).GetSummaryAsync(fixture.Customer.Id, request);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Booking.NotServiceable");
        }
    }

    /// <summary>
    /// The address the professional is sent to must belong to the area the
    /// slots and price were computed for. The default address is pre-selected
    /// for the customer, so a saved address in another city was silently
    /// bookable against a slot no provider there could serve.
    /// </summary>
    [Fact]
    public async Task An_address_outside_the_locality_being_booked_is_rejected()
    {
        using var context = _db.CreateContext();
        var fixture = Seed(context);

        // The address itself moves to a different pincode; the request still
        // books the original, serviceable locality.
        var elsewherePincode = new Pincode(Guid.NewGuid(), fixture.City.Id, Guid.NewGuid().ToString("N")[..6]);
        var elsewhereZone = new Zone(Guid.NewGuid(), fixture.City.Id, "Elsewhere");
        var elsewhereLocality = new Locality(Guid.NewGuid(), elsewhereZone.Id, elsewherePincode.Id, "Elsewhere");
        context.Pincodes.Add(elsewherePincode);
        context.Zones.Add(elsewhereZone);
        context.Localities.Add(elsewhereLocality);

        var address = context.Set<CustomerAddress>().Single(a => a.Id == fixture.Address.Id);
        address.LinkToGeography(elsewherePincode.Id, elsewhereLocality.Id);
        context.SaveChanges();

        var result = await BuildService(context).GetSummaryAsync(fixture.Customer.Id, RequestFor(fixture));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.AddressOutsideSelectedArea");
    }

    /// <summary>
    /// CityId drives city-specific pricing, so accepting one that disagrees
    /// with the locality is a price-manipulation vector, not just an
    /// inconsistency.
    /// </summary>
    [Fact]
    public async Task A_city_that_does_not_match_the_locality_is_rejected()
    {
        using var context = _db.CreateContext();
        var fixture = Seed(context);

        var otherCity = new City(Guid.NewGuid(), fixture.State.Id, "Mysuru");
        context.Cities.Add(otherCity);
        context.SaveChanges();

        var result = await BuildService(context).GetSummaryAsync(
            fixture.Customer.Id, RequestFor(fixture) with { CityId = otherCity.Id });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.CityMismatch");
    }

    [Fact]
    public async Task A_quantity_above_the_configured_maximum_is_rejected()
    {
        using var context = _db.CreateContext();
        var fixture = Seed(context);

        var result = await BuildService(context, TestServices.BookingOptions(maxQuantityPerBooking: 3))
            .GetSummaryAsync(fixture.Customer.Id, RequestFor(fixture) with { Quantity = 4 });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.QuantityTooLarge");
    }

    [Fact]
    public async Task A_slot_window_id_that_is_not_actually_offered_is_rejected()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = Seed(context);
        }

        using var readContext = _db.CreateContext();
        var request = RequestFor(fixture) with { SlotWindowId = Guid.NewGuid() };
        var result = await BuildService(readContext).GetSummaryAsync(fixture.Customer.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.SlotNotAvailable");
    }
}
