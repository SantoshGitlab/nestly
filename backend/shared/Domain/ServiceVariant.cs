using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A priced, timed option a <see cref="Service"/> can be booked as (Phase 3
/// catalog redesign - e.g. AC Repair offering "Split AC" vs "Window AC" as
/// separate priced/duration options). Purely additive: a service with zero
/// variants keeps using its own flat <see cref="Service.Price"/>/<see cref="Service.DurationMinutes"/>
/// exactly as before this entity existed. Shaped as a plain child entity of
/// Service - own table, own repository, FK-like Guid column - matching
/// <see cref="ServiceMedia"/>/<see cref="ServiceFaq"/>'s convention rather
/// than Service's own aggregate-root convention, since a variant has no
/// lifecycle or invariant of its own beyond its parent service's existence.
/// </summary>
public class ServiceVariant : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? InclusionsOverride { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    protected ServiceVariant() { }

    public ServiceVariant(Guid id, Guid serviceId, string name, decimal price, int durationMinutes) : base(id)
    {
        ServiceId = serviceId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");
        DurationMinutes = durationMinutes > 0
            ? durationMinutes
            : throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be positive.");
        IsActive = true;
        SortOrder = 0;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetPrice(decimal price) => Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");

    public void SetDuration(int durationMinutes) => DurationMinutes = durationMinutes > 0
        ? durationMinutes
        : throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be positive.");

    public void SetInclusionsOverride(string? inclusionsOverride) => InclusionsOverride = inclusionsOverride;
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
