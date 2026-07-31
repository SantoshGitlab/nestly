using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// City-specific override of a service's base price (SRS 12.8.1 "City-wise
/// price"). Absence of a row for a (service, city) pair means the service's
/// own Price applies everywhere in that city.
///
/// <see cref="EffectiveStartDate"/>/<see cref="EffectiveEndDate"/> support
/// time-bound city price changes (SRS 12.8.2 "Effective date support"). A
/// null <see cref="EffectiveEndDate"/> means the override has no expiry.
/// There is deliberately no delete operation: retiring an override by
/// setting an end date in the past preserves history instead of destroying
/// it, in keeping with SRS 12.8.2's price-change-audit requirement.
/// </summary>
public class ServiceCityPrice : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public Guid CityId { get; private set; }
    public decimal Price { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }

    protected ServiceCityPrice() { }

    public ServiceCityPrice(
        Guid id,
        Guid serviceId,
        Guid cityId,
        decimal price,
        DateOnly? effectiveStartDate = null,
        DateOnly? effectiveEndDate = null)
        : base(id)
    {
        ServiceId = serviceId;
        CityId = cityId;
        SetPrice(price);
        SetEffectiveDateRange(effectiveStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow), effectiveEndDate);
    }

    public void SetPrice(decimal price)
    {
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    }

    public void SetEffectiveDateRange(DateOnly startDate, DateOnly? endDate)
    {
        if (endDate.HasValue && startDate > endDate.Value)
        {
            throw new ArgumentException("The effective start date must not be after the effective end date.", nameof(startDate));
        }

        EffectiveStartDate = startDate;
        EffectiveEndDate = endDate;
    }

    /// <summary>Whether this override is in force on the given date.</summary>
    public bool IsEffectiveOn(DateOnly date) =>
        date >= EffectiveStartDate && (!EffectiveEndDate.HasValue || date <= EffectiveEndDate.Value);
}
