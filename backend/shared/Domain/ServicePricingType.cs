namespace Nestly.Domain;

/// <summary>
/// How a <see cref="Service"/> is priced (SRS 12.6.3): a flat package price,
/// or a base price that add-ons/quantity vary on top of. Stored as its
/// string name (see <c>ServiceConfiguration</c>) so the column stays
/// readable in the database and stable across enum member reordering.
/// </summary>
public enum ServicePricingType
{
    /// <summary>A single fixed package price; no variation.</summary>
    Fixed,

    /// <summary>Base price plus optional add-ons and/or quantity (SRS 12.6.3).</summary>
    Variable
}
