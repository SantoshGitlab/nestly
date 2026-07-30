using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Per-city pricing settings (SRS 11.9.1/12.8.1): visit charge, tax rate,
/// and platform fee. One row per city - all three are simple scalar
/// settings with no sub-structure, so one policy entity covers them rather
/// than three near-empty tables (same reasoning as SlotBookingPolicy).
/// </summary>
public class CityPricingPolicy : Entity<Guid>
{
    public Guid CityId { get; private set; }

    /// <summary>Flat visit/inspection charge added to every booking in this city.</summary>
    public decimal VisitCharge { get; private set; }

    /// <summary>Tax/GST rate as a percentage (e.g. 18 for 18%).</summary>
    public decimal TaxPercentage { get; private set; }

    /// <summary>Flat convenience/platform fee (SRS 11.9.1, optional - zero means none configured).</summary>
    public decimal PlatformFee { get; private set; }

    protected CityPricingPolicy() { }

    public CityPricingPolicy(Guid id, Guid cityId, decimal visitCharge, decimal taxPercentage, decimal platformFee) : base(id)
    {
        CityId = cityId;
        SetVisitCharge(visitCharge);
        SetTaxPercentage(taxPercentage);
        SetPlatformFee(platformFee);
    }

    public void SetVisitCharge(decimal visitCharge) =>
        VisitCharge = visitCharge >= 0 ? visitCharge : throw new ArgumentOutOfRangeException(nameof(visitCharge));

    public void SetTaxPercentage(decimal taxPercentage) =>
        TaxPercentage = taxPercentage is >= 0 and <= 100
            ? taxPercentage
            : throw new ArgumentOutOfRangeException(nameof(taxPercentage), "Tax percentage must be between 0 and 100.");

    public void SetPlatformFee(decimal platformFee) =>
        PlatformFee = platformFee >= 0 ? platformFee : throw new ArgumentOutOfRangeException(nameof(platformFee));
}
