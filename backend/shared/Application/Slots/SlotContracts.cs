namespace Nestly.Application.Slots;

/// <summary>A bookable window on the requested date (task 46, SRS 24.4).</summary>
public record SlotOptionResponse(
    Guid SlotWindowId,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int? MaxBookingsPerSlot);

/// <summary>
/// Slot availability for a service+address+date. IsServiceable is false only
/// when the service isn't offered at this address at all (SRS 12.9.2); an
/// empty Slots list with IsServiceable true means the date is out of range,
/// blacked out, or every window missed cutoff.
/// </summary>
public record SlotAvailabilityResponse(bool IsServiceable, IReadOnlyList<SlotOptionResponse> Slots);

public record SlotRevalidationResponse(bool IsValid, string? Reason);
