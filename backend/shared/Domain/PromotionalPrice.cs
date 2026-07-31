using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A time-bound promotional/discounted price for a service (SRS 12.8.1
/// "Promotional price", 12.8.2 "Effective date support"). Optionally scoped
/// to a single city; a null <see cref="CityId"/> means the promotion applies
/// nationally, on top of the service's base price or any
/// <see cref="ServiceCityPrice"/> override.
///
/// Distinct from <see cref="Coupon"/>: a coupon is a code the customer enters
/// at checkout, while a promotional price is a schedule the admin sets
/// directly on a service - no code required.
/// </summary>
public class PromotionalPrice : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public Guid? CityId { get; private set; }
    public decimal DiscountedPrice { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }

    protected PromotionalPrice() { }

    public PromotionalPrice(Guid id, Guid serviceId, Guid? cityId, decimal discountedPrice, DateOnly startDate, DateOnly endDate)
        : base(id)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("The start date must not be after the end date.", nameof(startDate));
        }

        ServiceId = serviceId;
        CityId = cityId;
        SetDiscountedPrice(discountedPrice);
        StartDate = startDate;
        EndDate = endDate;
        IsActive = true;
    }

    public void SetDiscountedPrice(decimal discountedPrice) =>
        DiscountedPrice = discountedPrice > 0 ? discountedPrice : throw new ArgumentOutOfRangeException(nameof(discountedPrice));

    public void SetDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("The start date must not be after the end date.", nameof(startDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>Whether this promotion is in force on the given date - active flag plus the [StartDate, EndDate] range.</summary>
    public bool IsEffectiveOn(DateOnly date) => IsActive && date >= StartDate && date <= EndDate;

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }
}
