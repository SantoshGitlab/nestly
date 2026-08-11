using FluentAssertions;
using Nestly.Application.Pricing;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 47a-c (base/add-on/quantity) and 48 (full breakdown API logic).</summary>
public sealed class PriceCalculationServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public PriceCalculationServiceTests(TestDatabase db) => _db = db;

    private PriceCalculationService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new ServiceRepository(context),
        new ServiceAddOnRepository(context),
        new ServiceabilityRepository(context),
        new ServiceCityPriceRepository(context),
        new CityPricingPolicyRepository(context),
        new ServiceVariantRepository(context),
        new ServiceAddOnGroupRepository(context));

    private (Category category, Service service, State state, City city) SeedServiceAndCity(Nestly.Infrastructure.Persistence.NestlyDbContext context, decimal basePrice = 500m)
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

    [Fact]
    public async Task Base_price_times_quantity_with_no_addons_or_policy()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 2, []));

        result.IsSuccess.Should().BeTrue();
        result.Value.BasePrice.Should().Be(500m);
        result.Value.BaseTotal.Should().Be(1000m);
        result.Value.TotalPayable.Should().Be(1000m);
    }

    [Fact]
    public async Task Addon_line_items_are_included_in_the_total()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 150m);
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 2)]));

        result.IsSuccess.Should().BeTrue();
        result.Value.AddOnLineItems.Should().ContainSingle(a => a.LineTotal == 300m);
        result.Value.AddOnTotal.Should().Be(300m);
        result.Value.TotalPayable.Should().Be(800m);
    }

    [Fact]
    public async Task An_addon_belonging_to_a_different_service_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, serviceOne, _, city) = SeedServiceAndCity(context, 500m);
        var category2 = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var serviceTwo = new Service(Guid.NewGuid(), category2.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 400m);
        var foreignAddOn = new ServiceAddOn(Guid.NewGuid(), serviceTwo.Id, "Gas Refill", 200m);
        context.Add(category2);
        context.Add(serviceTwo);
        context.Add(foreignAddOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(serviceOne.Id, city.Id, 1, [new AddOnSelection(foreignAddOn.Id, 1)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.InvalidAddOn");
    }

    [Fact]
    public async Task City_override_price_wins_over_the_services_base_price()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        context.ServiceCityPrices.Add(new ServiceCityPrice(Guid.NewGuid(), service.Id, city.Id, 650m));
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 1, []));

        result.Value.BasePrice.Should().Be(650m);
    }

    [Fact]
    public async Task Visit_charge_tax_and_platform_fee_apply_on_top_of_the_subtotal()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        context.CityPricingPolicies.Add(new CityPricingPolicy(Guid.NewGuid(), city.Id, 50m, 18m, 10m));
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 1, []));

        // subtotal = 500 (base) + 50 (visit) = 550; tax = 18% of 550 = 99; total = 550 + 99 + 10 = 659
        result.Value.Subtotal.Should().Be(550m);
        result.Value.TaxAmount.Should().Be(99m);
        result.Value.TotalPayable.Should().Be(659m);
    }

    [Fact]
    public async Task Zero_or_negative_quantity_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context);

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 0, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.InvalidQuantity");
    }

    [Fact]
    public async Task Unknown_service_returns_not_found()
    {
        using var context = _db.CreateContext();
        var (_, _, _, city) = SeedServiceAndCity(context);

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(Guid.NewGuid(), city.Id, 1, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.ServiceNotFound");
    }

    [Fact]
    public async Task Unknown_city_returns_not_found()
    {
        using var context = _db.CreateContext();
        var (_, service, _, _) = SeedServiceAndCity(context);

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, Guid.NewGuid(), 1, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.CityNotFound");
    }

    [Fact]
    public async Task Zero_or_negative_addon_quantity_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 150m);
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 0)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.InvalidAddOnQuantity");
    }

    [Fact]
    public async Task An_inactive_service_returns_not_found()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context);
        service.Deactivate();
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 1, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.ServiceNotFound");
    }

    [Fact]
    public async Task An_inactive_addon_is_rejected_even_though_it_belongs_to_the_right_service()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 150m);
        addOn.Deactivate();
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 1)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.InvalidAddOn");
    }

    // ---- Phase 3 catalog redesign: variants + grouped add-ons ----

    [Fact]
    public async Task A_selected_variants_price_and_duration_win_over_the_services_flat_price()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var variant = new ServiceVariant(Guid.NewGuid(), service.Id, "Split AC", 799m, 90);
        context.Add(variant);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [], variant.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.BasePrice.Should().Be(799m);
        result.Value.SelectedVariantId.Should().Be(variant.Id);
        result.Value.SelectedVariantName.Should().Be("Split AC");
        result.Value.SelectedVariantDurationMinutes.Should().Be(90);
    }

    [Fact]
    public async Task A_city_price_override_is_ignored_when_a_variant_is_selected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var variant = new ServiceVariant(Guid.NewGuid(), service.Id, "Split AC", 799m, 90);
        context.Add(variant);
        context.ServiceCityPrices.Add(new ServiceCityPrice(Guid.NewGuid(), service.Id, city.Id, 650m));
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [], variant.Id));

        result.Value.BasePrice.Should().Be(799m);
    }

    [Fact]
    public async Task An_inactive_variant_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var variant = new ServiceVariant(Guid.NewGuid(), service.Id, "Split AC", 799m, 90);
        variant.Deactivate();
        context.Add(variant);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(service.Id, city.Id, 1, [], variant.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.VariantNotFound");
    }

    [Fact]
    public async Task A_variant_belonging_to_a_different_service_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, serviceOne, _, city) = SeedServiceAndCity(context, 500m);
        var category2 = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var serviceTwo = new Service(Guid.NewGuid(), category2.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 400m);
        var foreignVariant = new ServiceVariant(Guid.NewGuid(), serviceTwo.Id, "Window AC", 399m, 60);
        context.Add(category2);
        context.Add(serviceTwo);
        context.Add(foreignVariant);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(
            new PriceCalculationRequest(serviceOne.Id, city.Id, 1, [], foreignVariant.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.VariantNotFound");
    }

    [Fact]
    public async Task A_request_with_no_variant_id_behaves_identically_to_before_variants_existed()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var variant = new ServiceVariant(Guid.NewGuid(), service.Id, "Split AC", 799m, 90);
        context.Add(variant);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(service.Id, city.Id, 2, []));

        result.IsSuccess.Should().BeTrue();
        result.Value.BasePrice.Should().Be(500m);
        result.Value.SelectedVariantId.Should().BeNull();
        result.Value.SelectedVariantName.Should().BeNull();
    }

    [Fact]
    public async Task Selecting_two_addons_from_a_single_selection_group_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), service.Id, "Detergent", AddOnGroupSelectionType.Single);
        var addOnA = new ServiceAddOn(Guid.NewGuid(), service.Id, "Powder", 50m);
        addOnA.SetGroupId(group.Id);
        var addOnB = new ServiceAddOn(Guid.NewGuid(), service.Id, "Liquid", 60m);
        addOnB.SetGroupId(group.Id);
        context.Add(group);
        context.Add(addOnA);
        context.Add(addOnB);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(
            service.Id, city.Id, 1, [new AddOnSelection(addOnA.Id, 1), new AddOnSelection(addOnB.Id, 1)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupSingleSelectionViolated");
    }

    [Fact]
    public async Task Selecting_more_addons_than_a_groups_max_select_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), service.Id, "Extras", AddOnGroupSelectionType.Multiple);
        group.SetSelectionRule(minSelect: 0, maxSelect: 1);
        var addOnA = new ServiceAddOn(Guid.NewGuid(), service.Id, "A", 50m);
        addOnA.SetGroupId(group.Id);
        var addOnB = new ServiceAddOn(Guid.NewGuid(), service.Id, "B", 60m);
        addOnB.SetGroupId(group.Id);
        context.Add(group);
        context.Add(addOnA);
        context.Add(addOnB);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(
            service.Id, city.Id, 1, [new AddOnSelection(addOnA.Id, 1), new AddOnSelection(addOnB.Id, 1)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupMaxSelectExceeded");
    }

    [Fact]
    public async Task Selecting_fewer_addons_than_a_groups_min_select_is_rejected()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), service.Id, "Required extras", AddOnGroupSelectionType.Multiple);
        group.SetSelectionRule(minSelect: 2, maxSelect: null);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "A", 50m);
        addOn.SetGroupId(group.Id);
        context.Add(group);
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(
            service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 1)]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupMinSelectNotMet");
    }

    [Fact]
    public async Task A_valid_single_selection_from_a_group_is_priced_and_carries_the_group_name()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), service.Id, "Detergent", AddOnGroupSelectionType.Single);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Powder", 50m);
        addOn.SetGroupId(group.Id);
        context.Add(group);
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(
            service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 1)]));

        result.IsSuccess.Should().BeTrue();
        var line = result.Value.AddOnLineItems.Should().ContainSingle().Subject;
        line.GroupId.Should().Be(group.Id);
        line.GroupName.Should().Be("Detergent");
    }

    [Fact]
    public async Task An_ungrouped_addon_selection_carries_no_group_info_same_as_before_groups_existed()
    {
        using var context = _db.CreateContext();
        var (_, service, _, city) = SeedServiceAndCity(context, 500m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Standalone", 50m);
        context.Add(addOn);
        context.SaveChanges();

        var result = await BuildService(context).CalculateAsync(new PriceCalculationRequest(
            service.Id, city.Id, 1, [new AddOnSelection(addOn.Id, 1)]));

        var line = result.Value.AddOnLineItems.Should().ContainSingle().Subject;
        line.GroupId.Should().BeNull();
        line.GroupName.Should().BeNull();
    }
}
