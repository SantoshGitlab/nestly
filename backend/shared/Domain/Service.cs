using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

public class Service : AggregateRoot<Guid>
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>SEO-friendly identifier (SRS 12.6.2), globally unique like Category's.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>What the service covers (SRS 12.6.2), shown on the service detail page.</summary>
    public string Inclusions { get; private set; } = string.Empty;

    /// <summary>What the service explicitly does not cover (SRS 12.6.2).</summary>
    public string Exclusions { get; private set; } = string.Empty;

    /// <summary>Cancellation policy summary shown on the detail page (SRS 12.6.2, 11.6.1).</summary>
    public string? CancellationPolicy { get; private set; }

    /// <summary>Reschedule policy summary shown on the detail page (SRS 12.6.2, 11.6.1).</summary>
    public string? ReschedulePolicy { get; private set; }

    protected Service() { }

    public Service(Guid id, Guid categoryId, string name, string slug, string description, decimal price) : base(id)
    {
        CategoryId = categoryId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Slug = slug ?? throw new ArgumentNullException(nameof(slug));
        Description = description ?? string.Empty;
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        IsActive = true;
        RaiseDomainEvent(new ServiceCreatedEvent(Id, CategoryId));
    }

    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetSlug(string slug) => Slug = slug ?? throw new ArgumentNullException(nameof(slug));
    public void SetDescription(string d) => Description = d ?? string.Empty;

    public void SetPrice(decimal price)
    {
        decimal validated = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        if (validated == Price) return;
        decimal oldPrice = Price;
        Price = validated;
        RaiseDomainEvent(new ServicePriceChangedEvent(Id, oldPrice, Price));
    }

    public void SetInclusions(string inclusions) => Inclusions = inclusions ?? string.Empty;
    public void SetExclusions(string exclusions) => Exclusions = exclusions ?? string.Empty;
    public void SetCancellationPolicy(string? policy) => CancellationPolicy = policy;
    public void SetReschedulePolicy(string? policy) => ReschedulePolicy = policy;

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        RaiseDomainEvent(new ServiceActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        RaiseDomainEvent(new ServiceDeactivatedEvent(Id));
    }
}
