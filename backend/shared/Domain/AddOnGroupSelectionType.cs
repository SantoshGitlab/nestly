namespace Nestly.Domain;

/// <summary>
/// How many add-ons a customer may pick from within one <see cref="ServiceAddOnGroup"/>
/// (Phase 3 catalog redesign). Stored as its string name (see
/// <c>ServiceAddOnGroupConfiguration</c>) so the column stays readable in the
/// database and stable across enum member reordering - same convention as
/// <see cref="ServicePricingType"/>.
/// </summary>
public enum AddOnGroupSelectionType
{
    /// <summary>At most one add-on from this group may be selected (radio-button UI).</summary>
    Single,

    /// <summary>More than one add-on from this group may be selected, bounded by <see cref="ServiceAddOnGroup.MinSelect"/>/<see cref="ServiceAddOnGroup.MaxSelect"/> (checkbox UI).</summary>
    Multiple
}
