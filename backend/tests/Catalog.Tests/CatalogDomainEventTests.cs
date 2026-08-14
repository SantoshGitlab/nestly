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
    public void Marking_a_service_updated_raises_ServiceUpdatedEvent_with_old_and_new_category()
    {
        var category = new Category(Guid.NewGuid(), "Repairs3", "repairs-3-" + Guid.NewGuid(), "desc");
        var newCategory = new Category(Guid.NewGuid(), "Repairs4", "repairs-4-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Washer Repair", "washer-repair-" + Guid.NewGuid(), "desc", 300m);
        service.ClearDomainEvents();

        service.SetCategoryId(newCategory.Id);
        service.MarkUpdated(category.Id);

        var raised = service.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceUpdatedEvent>().Subject;
        raised.ServiceId.Should().Be(service.Id);
        raised.OldCategoryId.Should().Be(category.Id);
        raised.NewCategoryId.Should().Be(newCategory.Id);
    }

    [Fact]
    public void Creating_an_addon_raises_ServiceAddOnCreatedEvent()
    {
        var addOn = new ServiceAddOn(Guid.NewGuid(), Guid.NewGuid(), "Extra Filter", 50m);

        addOn.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceAddOnCreatedEvent>()
            .Which.ServiceAddOnId.Should().Be(addOn.Id);
    }

    [Fact]
    public void Setting_a_categorys_parent_raises_CategoryParentChangedEvent_with_old_and_new_parent()
    {
        var category = new Category(Guid.NewGuid(), "Kitchen Cleaning", "kitchen-cleaning-" + Guid.NewGuid(), "desc");
        var parentId = Guid.NewGuid();
        category.ClearDomainEvents();

        category.SetParent(parentId);

        var raised = category.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CategoryParentChangedEvent>().Subject;
        raised.OldParentCategoryId.Should().BeNull();
        raised.NewParentCategoryId.Should().Be(parentId);
    }

    [Fact]
    public void Setting_a_categorys_parent_to_its_current_value_does_not_raise_an_event()
    {
        var parentId = Guid.NewGuid();
        var category = new Category(Guid.NewGuid(), "Bathroom Cleaning", "bathroom-cleaning-" + Guid.NewGuid(), "desc");
        category.SetParent(parentId);
        category.ClearDomainEvents();

        category.SetParent(parentId);

        category.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_category_cannot_be_set_as_its_own_parent()
    {
        var category = new Category(Guid.NewGuid(), "Sofa Cleaning", "sofa-cleaning-" + Guid.NewGuid(), "desc");

        var act = () => category.SetParent(category.Id);

        act.Should().Throw<ArgumentException>();
    }
}
