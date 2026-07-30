using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class Service : AggregateRoot<Guid>
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>What the service covers (SRS 12.6.2), shown on the service detail page.</summary>
    public string Inclusions { get; private set; } = string.Empty;

    /// <summary>What the service explicitly does not cover (SRS 12.6.2).</summary>
    public string Exclusions { get; private set; } = string.Empty;

    protected Service() { }

    public Service(Guid id, Guid categoryId, string name, string description, decimal price) : base(id)
    {
        CategoryId = categoryId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        IsActive = true;
    }

    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetDescription(string d) => Description = d ?? string.Empty;
    public void SetPrice(decimal price) => Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    public void SetInclusions(string inclusions) => Inclusions = inclusions ?? string.Empty;
    public void SetExclusions(string exclusions) => Exclusions = exclusions ?? string.Empty;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
