using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An admin-configured Annual Maintenance Contract catalog entry (docs/AMC.md):
/// what category of asset it covers, how much it costs, how long the term
/// runs, and how many service visits are included. Mirrors
/// <see cref="SubscriptionPlan"/>'s split - this entity owns only rules that
/// are pure functions of its own fields; everything I/O-dependent (name
/// uniqueness, active-contract counts) lives in <c>IAmcAdminService</c>.
/// </summary>
public class AmcPlan : Entity<Guid>
{
    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    /// <summary>Contract term length in months from purchase (docs/AMC.md's "typically 12").</summary>
    public int TermMonths { get; private set; }

    /// <summary>Total service visits a purchased contract starts with (docs/AMC.md's entitlement count).</summary>
    public int VisitsIncluded { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByAdminUserId { get; private set; }

    protected AmcPlan() { }

    public AmcPlan(
        Guid id,
        Guid categoryId,
        string name,
        string? description,
        decimal price,
        int termMonths,
        int visitsIncluded)
        : base(id)
    {
        ValidateFields(name, price, termMonths, visitsIncluded);

        CategoryId = categoryId;
        Name = name.Trim();
        Description = description;
        Price = price;
        TermMonths = termMonths;
        VisitsIncluded = visitsIncluded;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Edits every mutable field (admin CRUD). Same validation as the constructor. Existing contracts are unaffected - their terms were already snapshotted at purchase (see <see cref="CustomerAmcContract"/>), the same "an edit never reprices an existing holder" convention <see cref="SubscriptionPlan.Update"/> establishes.</summary>
    public void Update(
        Guid categoryId,
        string name,
        string? description,
        decimal price,
        int termMonths,
        int visitsIncluded,
        Guid? updatedByAdminUserId)
    {
        ValidateFields(name, price, termMonths, visitsIncluded);

        CategoryId = categoryId;
        Name = name.Trim();
        Description = description;
        Price = price;
        TermMonths = termMonths;
        VisitsIncluded = visitsIncluded;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    /// <summary>Re-enables a plan for new purchases - existing contracts were never affected by deactivation (see <see cref="Deactivate"/>).</summary>
    public void Activate(Guid? updatedByAdminUserId)
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    /// <summary>Suspends a plan without deleting it - mirrors <see cref="SubscriptionPlan.Deactivate"/>: no new purchases can start on it, but every existing <see cref="CustomerAmcContract"/> already on this plan (its terms already snapshotted) is unaffected.</summary>
    public void Deactivate(Guid? updatedByAdminUserId)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void ValidateFields(string name, decimal price, int termMonths, int visitsIncluded)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plan name is required.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Plan price must be positive - an AMC is a prepaid purchase, unlike a zero-price Subscription discount tier.");
        }

        if (termMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(termMonths), "Term months must be positive.");
        }

        if (visitsIncluded <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitsIncluded), "Visits included must be positive - a plan with zero visits has nothing to entitle.");
        }
    }
}
