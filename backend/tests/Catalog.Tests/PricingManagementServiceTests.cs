using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Pricing;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Admin pricing management (SRS 12.8, tasks 109a-109e), exercised end to end
/// through <see cref="PricingManagementService"/> over a real relational
/// database - same rationale as <c>SystemSettingsServiceTests</c>: the audit
/// row and the pricing change both need to commit in the same transaction, so
/// a stubbed repository would not prove that.
/// </summary>
public sealed class PricingManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;
    private readonly Guid _actorId = Guid.NewGuid();

    public PricingManagementServiceTests(TestDatabase db) => _db = db;

    private PricingManagementService CreateService(NestlyDbContext context) => new(
        new ServiceRepository(context),
        new ServiceAddOnRepository(context),
        new CityRepository(context),
        new ServiceCityPriceRepository(context),
        new PromotionalPriceRepository(context),
        new CityPricingPolicyRepository(context),
        new AuditLogWriter(context, new StubAuditContextProvider(_actorId)),
        new StubAuditContextProvider(_actorId));

    private (Category category, Service service, State state, City city) SeedServiceAndCity(NestlyDbContext context, decimal basePrice = 500m)
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", basePrice);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");

        context.Add(category);
        context.Add(service);
        context.States.Add(state);
        context.Cities.Add(city);
        context.SaveChanges();

        return (category, service, state, city);
    }

    // ---- Base price ----

    [Fact]
    public async Task UpdateServicePriceAsync_persists_the_new_price_and_writes_an_audit_entry()
    {
        Guid serviceId;
        using (var context = _db.CreateContext())
        {
            var (_, service, _, _) = SeedServiceAndCity(context, 500m);
            serviceId = service.Id;

            var result = await CreateService(context).UpdateServicePriceAsync(serviceId, new ServicePriceUpdateRequest(650m));
            result.IsSuccess.Should().BeTrue();
            result.Value.Price.Should().Be(650m);
        }

        using var readContext = _db.CreateContext();
        readContext.Set<Service>().Single(s => s.Id == serviceId).Price.Should().Be(650m);

        var auditRows = readContext.Set<AuditLog>().Where(a => a.EntityName == "Service" && a.EntityId == serviceId.ToString()).ToList();
        auditRows.Should().ContainSingle();
        auditRows[0].Action.Should().Be("PriceChanged");
        auditRows[0].ActorId.Should().Be(_actorId);
        auditRows[0].OldValues.Should().Contain("500");
        auditRows[0].NewValues.Should().Contain("650");
    }

    [Fact]
    public async Task UpdateServicePriceAsync_returns_not_found_for_an_unknown_service()
    {
        using var context = _db.CreateContext();

        var result = await CreateService(context).UpdateServicePriceAsync(Guid.NewGuid(), new ServicePriceUpdateRequest(100m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.ServiceNotFound");
    }

    // ---- Add-on price ----

    [Fact]
    public async Task UpdateAddOnPriceAsync_persists_the_new_price()
    {
        Guid addOnId;
        using (var context = _db.CreateContext())
        {
            var (_, service, _, _) = SeedServiceAndCity(context);
            var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Extra Fridge", 150m);
            context.Add(addOn);
            context.SaveChanges();
            addOnId = addOn.Id;

            var result = await CreateService(context).UpdateAddOnPriceAsync(addOnId, new AddOnPriceUpdateRequest(200m));
            result.IsSuccess.Should().BeTrue();
            result.Value.Price.Should().Be(200m);
        }

        using var readContext = _db.CreateContext();
        readContext.Set<ServiceAddOn>().Single(a => a.Id == addOnId).Price.Should().Be(200m);
    }

    [Fact]
    public async Task ListAddOnPricesAsync_filters_by_service_when_requested()
    {
        using var context = _db.CreateContext();
        var (_, serviceA, _, _) = SeedServiceAndCity(context);
        var (_, serviceB, _, _) = SeedServiceAndCity(context);
        context.Add(new ServiceAddOn(Guid.NewGuid(), serviceA.Id, "A add-on", 50m));
        context.Add(new ServiceAddOn(Guid.NewGuid(), serviceB.Id, "B add-on", 60m));
        context.SaveChanges();

        var results = await CreateService(context).ListAddOnPricesAsync(serviceA.Id);

        results.Should().ContainSingle(a => a.ServiceId == serviceA.Id);
    }

    // ---- City-wise price ----

    [Fact]
    public async Task CreateCityPriceAsync_creates_an_override_with_effective_dates()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context);
        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 8, 31);

        var result = await CreateService(context).CreateCityPriceAsync(
            new CityPriceCreateRequest(service.Id, city.Id, 599m, start, end));

        result.IsSuccess.Should().BeTrue();
        result.Value.Price.Should().Be(599m);
        result.Value.EffectiveStartDate.Should().Be(start);
        result.Value.EffectiveEndDate.Should().Be(end);
        result.Value.ServiceName.Should().Be(service.Name);
        result.Value.CityName.Should().Be(city.Name);
    }

    [Fact]
    public async Task CreateCityPriceAsync_rejects_a_duplicate_service_and_city_pair()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context);
        var pricingService = CreateService(context);
        (await pricingService.CreateCityPriceAsync(new CityPriceCreateRequest(service.Id, city.Id, 599m, null, null))).IsSuccess.Should().BeTrue();

        var result = await pricingService.CreateCityPriceAsync(new CityPriceCreateRequest(service.Id, city.Id, 650m, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.DuplicateCityPrice");
    }

    [Fact]
    public async Task UpdateCityPriceAsync_changes_price_and_effective_window_and_writes_audit()
    {
        Guid cityPriceId;
        using (var context = _db.CreateContext())
        {
            var (_, service, _, city) = SeedServiceAndCity(context);
            var created = await CreateService(context).CreateCityPriceAsync(new CityPriceCreateRequest(service.Id, city.Id, 500m, null, null));
            cityPriceId = created.Value.Id;

            var newStart = new DateOnly(2026, 9, 1);
            var newEnd = new DateOnly(2026, 9, 30);
            var updated = await CreateService(context).UpdateCityPriceAsync(cityPriceId, new CityPriceUpdateRequest(575m, newStart, newEnd));

            updated.IsSuccess.Should().BeTrue();
            updated.Value.Price.Should().Be(575m);
            updated.Value.EffectiveStartDate.Should().Be(newStart);
            updated.Value.EffectiveEndDate.Should().Be(newEnd);
        }

        using var readContext = _db.CreateContext();
        var auditRows = readContext.Set<AuditLog>().Where(a => a.EntityName == "ServiceCityPrice" && a.EntityId == cityPriceId.ToString()).ToList();
        auditRows.Should().Contain(a => a.Action == "Created");
        auditRows.Should().Contain(a => a.Action == "Updated");
    }

    // ---- Promotional price ----

    [Fact]
    public async Task CreatePromotionalPriceAsync_supports_a_national_promotion_with_no_city()
    {
        using var context = _db.CreateContext();
        var (_, service, _, _) = SeedServiceAndCity(context);
        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 8, 15);

        var result = await CreateService(context).CreatePromotionalPriceAsync(
            new PromotionalPriceCreateRequest(service.Id, null, 399m, start, end));

        result.IsSuccess.Should().BeTrue();
        result.Value.CityId.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
        result.Value.DiscountedPrice.Should().Be(399m);
    }

    [Fact]
    public async Task SetPromotionalPriceActiveAsync_toggles_the_active_flag_and_writes_audit()
    {
        Guid promoId;
        using (var context = _db.CreateContext())
        {
            var (_, service, _, city) = SeedServiceAndCity(context);
            var created = await CreateService(context).CreatePromotionalPriceAsync(
                new PromotionalPriceCreateRequest(service.Id, city.Id, 299m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
            promoId = created.Value.Id;

            var deactivated = await CreateService(context).SetPromotionalPriceActiveAsync(promoId, false);
            deactivated.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        readContext.Set<PromotionalPrice>().Single(p => p.Id == promoId).IsActive.Should().BeFalse();

        var auditRows = readContext.Set<AuditLog>().Where(a => a.EntityName == "PromotionalPrice" && a.EntityId == promoId.ToString()).ToList();
        auditRows.Should().Contain(a => a.Action == "Deactivated");
    }

    [Fact]
    public async Task CreatePromotionalPriceAsync_returns_not_found_for_an_unknown_city()
    {
        using var context = _db.CreateContext();
        var (_, service, _, _) = SeedServiceAndCity(context);

        var result = await CreateService(context).CreatePromotionalPriceAsync(
            new PromotionalPriceCreateRequest(service.Id, Guid.NewGuid(), 299m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.CityNotFound");
    }

    // ---- City pricing policy: tax + fees ----

    [Fact]
    public async Task UpsertCityPricingPolicyAsync_creates_then_updates_the_same_policy()
    {
        using var context = _db.CreateContext();
        var (_, _, _, city) = SeedServiceAndCity(context);
        var pricingService = CreateService(context);

        var created = await pricingService.UpsertCityPricingPolicyAsync(city.Id, new CityPricingPolicyUpsertRequest(99m, 18m, 15m));
        created.IsSuccess.Should().BeTrue();
        created.Value.TaxPercentage.Should().Be(18m);

        var updated = await pricingService.UpsertCityPricingPolicyAsync(city.Id, new CityPricingPolicyUpsertRequest(120m, 12m, 20m));
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Id.Should().Be(created.Value.Id);
        updated.Value.VisitCharge.Should().Be(120m);
        updated.Value.TaxPercentage.Should().Be(12m);
        updated.Value.PlatformFee.Should().Be(20m);

        (await pricingService.ListCityPricingPoliciesAsync()).Should().ContainSingle(p => p.CityId == city.Id);
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        private readonly Guid? _actorId;

        public StubAuditContextProvider(Guid? actorId) => _actorId = actorId;

        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, _actorId, IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
