namespace Nestly.Application.PartnerAvailability;

/// <summary>One recurring weekly working window (PARTNER.md "partner_availability").</summary>
public record PartnerAvailabilityWindowResponse(
    Guid Id,
    Guid PartnerId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive);

public record PartnerAvailabilityWindowInput(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);

/// <summary>Full replacement of a partner's weekly schedule (PARTNER.md API surface "update availability").</summary>
public record UpdatePartnerAvailabilityWindowsRequest(IReadOnlyList<PartnerAvailabilityWindowInput> Windows);

/// <summary>One date range in which a partner is unavailable (PARTNER.md "blackout dates").</summary>
public record PartnerBlackoutDateResponse(
    Guid Id,
    Guid PartnerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason);

/// <summary>PARTNER.md API surface "set blackout dates".</summary>
public record AddPartnerBlackoutDateRequest(DateOnly StartDate, DateOnly EndDate, string? Reason);

/// <summary>Combined view returned by the availability "get" endpoint.</summary>
public record PartnerAvailabilityResponse(
    IReadOnlyList<PartnerAvailabilityWindowResponse> Windows,
    IReadOnlyList<PartnerBlackoutDateResponse> BlackoutDates);
