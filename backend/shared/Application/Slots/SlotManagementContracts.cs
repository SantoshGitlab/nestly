using Nestly.Domain;

namespace Nestly.Application.Slots;

/// <summary>
/// Admin request/response shapes for slot configuration (SRS 12.10, tasks
/// 113a-e): windows, blackouts, booking-policy cutoffs, per-window capacity,
/// and one-off availability overrides. Distinct from
/// <see cref="SlotOptionResponse"/>/<see cref="SlotAvailabilityResponse"/> in
/// <c>SlotContracts.cs</c>, which are the customer-facing availability
/// engine's (task 45a-d) output, not admin configuration of the rules behind it.
/// </summary>

// ---- Lookups (for the slot configuration screens' pickers) ----

/// <summary>
/// A lightweight active-city option for the slot configuration pickers.
/// Deliberately not <c>Nestly.Application.Geography.CityAdminResponse</c> -
/// that type carries state id/name/active-flag fields a picker doesn't need,
/// and depending on it would couple the Slots module's lookups to the
/// Serviceability module the same way <c>ServiceabilityMappingManagementService</c>
/// avoids depending on a Catalog controller for its own category/service
/// lookups (each module's admin screen must work for a role that holds only
/// that module's permission - see AdminPermissionCatalog's Booking Admin,
/// which grants slots but not serviceability).
/// </summary>
public sealed record SlotCityLookupResponse(Guid Id, string Name);

// ---- Windows (task 113a) ----

/// <summary>Admin view of a slot window, including the days of week it's offered on.</summary>
public sealed record SlotWindowAdminResponse(
    Guid Id,
    Guid CityId,
    string CityName,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive,
    int? MaxBookingsPerSlot,
    IReadOnlyList<DayOfWeek> DaysOfWeek);

public sealed record SlotWindowCreateRequest(
    Guid CityId,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int? MaxBookingsPerSlot,
    IReadOnlyList<DayOfWeek> DaysOfWeek);

/// <summary>
/// Update request. City is not editable after creation - <see cref="SlotWindow"/>
/// has no way to move a window to a different city, mirroring the geography
/// master's zone/locality rename-only convention.
/// </summary>
public sealed record SlotWindowUpdateRequest(
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<DayOfWeek> DaysOfWeek);

// ---- Capacity (task 113d) ----

/// <summary>
/// Sets or clears a window's per-day booking cap (SRS 12.10.1 "Max bookings
/// per slot if capacity is used"). A dedicated endpoint rather than folding
/// into <see cref="SlotWindowUpdateRequest"/> - capacity is changed far more
/// often, and independently of, a window's schedule.
/// </summary>
public sealed record SlotWindowCapacityUpdateRequest(int? MaxBookingsPerSlot);

// ---- Blackouts (task 113b) ----

public sealed record SlotBlackoutAdminResponse(
    Guid Id,
    Guid CityId,
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    SlotBlackoutType Type,
    string? Reason);

public sealed record SlotBlackoutCreateRequest(
    Guid CityId,
    DateOnly StartDate,
    DateOnly EndDate,
    SlotBlackoutType Type,
    string? Reason);

// ---- Cutoffs / advance-booking policy (task 113c) ----

public sealed record SlotBookingPolicyAdminResponse(Guid Id, Guid CityId, string CityName, int CutoffMinutes, int MaxAdvanceDays);

/// <summary>
/// Create-or-update: <see cref="SlotBookingPolicy"/> is one row per city by
/// its own invariant, so setting a city's cutoff/advance-booking rules is an
/// upsert rather than separate create and update calls.
/// </summary>
public sealed record SlotBookingPolicyUpsertRequest(Guid CityId, int CutoffMinutes, int MaxAdvanceDays);

// ---- Availability overrides (task 113e, SRS 12.10.2) ----

public sealed record SlotAvailabilityOverrideAdminResponse(
    Guid Id,
    Guid CityId,
    string CityName,
    DateOnly Date,
    Guid? SlotWindowId,
    string? SlotWindowName,
    Guid? CategoryId,
    string? CategoryName,
    Guid? ServiceId,
    string? ServiceName,
    string Reason);

/// <summary>
/// Blocks availability for a date, optionally narrowed to a slot window
/// and/or category/service (SRS 12.10.2). Leaving every optional field null
/// blocks the entire city for the whole day.
/// </summary>
public sealed record SlotAvailabilityOverrideCreateRequest(
    Guid CityId,
    DateOnly Date,
    Guid? SlotWindowId,
    Guid? CategoryId,
    Guid? ServiceId,
    string Reason);
