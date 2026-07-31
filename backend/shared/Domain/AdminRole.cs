using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A named collection of permissions assignable to admin users (SRS 12.2.2 —
/// Super Admin, Operations Admin, Booking Admin, Support Admin, Catalog Admin,
/// Pricing Admin, Marketing Admin, Finance Admin, Read-only Analyst). Roles
/// are configurable rather than hardcoded, so Super Admin can define new ones
/// beyond the seeded set.
/// </summary>
public class AdminRole : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected AdminRole() { }

    public AdminRole(Guid id, string name, string description) : base(id)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name;
        Description = description ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string description)
    {
        Description = description ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
}
