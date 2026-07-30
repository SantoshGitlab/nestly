using FluentAssertions;
using Nestly.Domain;
using Nestly.Domain.Events;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 40e (domain-level half): aggregates raise the expected
/// lifecycle events, and re-applying a no-op state change doesn't raise a
/// duplicate event. Dispatch through MediatR is covered separately in
/// <see cref="DomainEventDispatchTests"/>.
/// </summary>
public sealed class CatalogDomainEventTests
{
    [Fact]
    public void Creating_a_category_raises_CategoryCreatedEvent()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");

        category.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CategoryCreatedEvent>()
            .Which.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Deactivating_an_already_inactive_category_does_not_raise_a_duplicate_event()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        category.ClearDomainEvents();

        category.Deactivate();
        category.Deactivate();

        category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CategoryDeactivatedEvent>();
    }

    [Fact]
    public void Changing_a_services_price_raises_ServicePriceChangedEvent_with_old_and_new_price()
    {
        var category = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 500m);
        service.ClearDomainEvents();

        service.SetPrice(600m);

        var raised = service.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServicePriceChangedEvent>().Subject;
        raised.OldPrice.Should().Be(500m);
        raised.NewPrice.Should().Be(600m);
    }

    [Fact]
    public void Setting_the_same_price_again_does_not_raise_an_event()
    {
        var category = new Category(Guid.NewGuid(), "Repairs2", "repairs-2-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Fridge Repair", "fridge-repair-" + Guid.NewGuid(), "desc", 400m);
        service.ClearDomainEvents();

        service.SetPrice(400m);

        service.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Creating_an_addon_raises_ServiceAddOnCreatedEvent()
    {
        var addOn = new ServiceAddOn(Guid.NewGuid(), Guid.NewGuid(), "Extra Filter", 50m);

        addOn.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceAddOnCreatedEvent>()
            .Which.ServiceAddOnId.Should().Be(addOn.Id);
    }
}
