using Nestly.Application;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.Serviceability;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Slot availability calculation (tasks 45a-d, SRS 12.10). Single-timezone
/// assumption throughout - no per-city timezone field exists anywhere in the
/// geography schema; add one, and resolve <see cref="IBusinessClock"/> per
/// city, if the platform ever spans multiple timezones.
///
/// Every "is this still bookable" comparison goes through
/// <see cref="IBusinessClock"/> rather than <see cref="TimeProvider"/>
/// directly: stored slot times are business wall-clock values, and comparing
/// them against a UTC instant made the configured cutoff lenient by the
/// business timezone's offset.
/// </summary>
public class SlotAvailabilityService : ISlotAvailabilityService
{
    private readonly IServiceabilityRepository _serviceabilityRepository;
    private readonly IServiceabilityValidationService _serviceabilityValidationService;
    private readonly ISlotWindowRepository _slotWindowRepository;
    private readonly ISlotBlackoutRepository _blackoutRepository;
    private readonly ISlotBookingPolicyRepository _policyRepository;
    private readonly ISlotCapacityRepository _slotCapacityRepository;
    private readonly IBusinessClock _businessClock;

    public SlotAvailabilityService(
        IServiceabilityRepository serviceabilityRepository,
        IServiceabilityValidationService serviceabilityValidationService,
        ISlotWindowRepository slotWindowRepository,
        ISlotBlackoutRepository blackoutRepository,
        ISlotBookingPolicyRepository policyRepository,
        ISlotCapacityRepository slotCapacityRepository,
        IBusinessClock businessClock)
    {
        _serviceabilityRepository = serviceabilityRepository;
        _serviceabilityValidationService = serviceabilityValidationService;
        _slotWindowRepository = slotWindowRepository;
        _blackoutRepository = blackoutRepository;
        _policyRepository = policyRepository;
        _slotCapacityRepository = slotCapacityRepository;
        _businessClock = businessClock;
    }

    public async Task<Result<SlotAvailabilityResponse>> GetAvailableSlotsAsync(Guid serviceId, Guid localityId, DateOnly date)
    {
        var geo = await _serviceabilityRepository.GetCityAndPincodeForLocalityAsync(localityId);
        if (geo is null)
        {
            return Error.NotFound("Slots.LocalityNotFound", "The specified locality does not exist.");
        }

        var (cityId, pincodeId) = geo.Value;

        var serviceableResult = await _serviceabilityValidationService.IsServiceServiceableByPincodeAsync(serviceId, pincodeId);
        if (serviceableResult.IsFailure)
        {
            return serviceableResult.Error;
        }

        if (!serviceableResult.Value)
        {
            return new SlotAvailabilityResponse(IsServiceable: false, Slots: [], SlotUnavailabilityReason.NotServiceable);
        }

        // Business wall-clock on both sides of every comparison below: slot
        // dates and window start times are stored as local business time with
        // no offset, so measuring them against a UTC instant skews every
        // cutoff by the business timezone's offset (see IBusinessClock).
        DateTime now = _businessClock.Now;
        DateOnly today = _businessClock.Today;

        var policy = await _policyRepository.GetByCityAsync(cityId);
        int maxAdvanceDays = policy?.MaxAdvanceDays ?? int.MaxValue;
        int cutoffMinutes = policy?.CutoffMinutes ?? 0;

        if (date < today || (maxAdvanceDays != int.MaxValue && date > today.AddDays(maxAdvanceDays)))
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: [], SlotUnavailabilityReason.DateOutOfBookableRange);
        }

        var blackouts = await _blackoutRepository.ListInRangeAsync(cityId, date, date);
        if (blackouts.Any(b => b.CoversDate(date)))
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: [], SlotUnavailabilityReason.Blackout);
        }

        var windows = await _slotWindowRepository.ListActiveForCityAndDayAsync(cityId, date.DayOfWeek);
        if (windows.Count == 0)
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: [], SlotUnavailabilityReason.NoWindowsConfigured);
        }

        DateTime cutoffThreshold = now.AddMinutes(cutoffMinutes);

        var openWindows = windows
            .Where(w => date.ToDateTime(TimeOnly.MinValue).Add(w.StartTime) >= cutoffThreshold)
            .ToList();

        if (openWindows.Count == 0)
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: [], SlotUnavailabilityReason.CutoffPassed);
        }

        // Capacity is part of "is this bookable", not a surprise sprung at
        // creation time. Without this filter a window at MaxBookingsPerSlot
        // stayed selectable through the picker and through RevalidateSlotAsync
        // below, and only failed on the customer's final click, when
        // ReserveSlotAsync returned Booking.SlotCapacityReached.
        var slots = await FilterOutFullWindowsAsync(openWindows, date);

        return slots.Count == 0
            ? new SlotAvailabilityResponse(IsServiceable: true, Slots: [], SlotUnavailabilityReason.FullyBooked)
            : new SlotAvailabilityResponse(IsServiceable: true, Slots: slots);
    }

    /// <summary>
    /// Drops the windows that have already taken every seat they are allowed
    /// for <paramref name="date"/>. Windows with no configured capacity are
    /// unlimited and always survive; the counter lookup is one query for the
    /// whole set, and is skipped entirely when nothing is capped.
    /// </summary>
    private async Task<List<SlotOptionResponse>> FilterOutFullWindowsAsync(IReadOnlyList<SlotWindow> openWindows, DateOnly date)
    {
        var cappedWindowIds = openWindows
            .Where(w => w.MaxBookingsPerSlot is not null)
            .Select(w => w.Id)
            .ToList();

        if (cappedWindowIds.Count == 0)
        {
            return openWindows.Select(ToOption).ToList();
        }

        var bookedCounts = await _slotCapacityRepository.GetBookedCountsAsync(cappedWindowIds, date);

        return openWindows
            .Where(w => w.MaxBookingsPerSlot is null
                || !bookedCounts.TryGetValue(w.Id, out int booked)
                || booked < w.MaxBookingsPerSlot.Value)
            .Select(ToOption)
            .ToList();
    }

    public async Task<Result<SlotRevalidationResponse>> RevalidateSlotAsync(Guid serviceId, Guid localityId, Guid slotWindowId, DateOnly date)
    {
        var availability = await GetAvailableSlotsAsync(serviceId, localityId, date);
        if (availability.IsFailure)
        {
            return availability.Error;
        }

        if (!availability.Value.IsServiceable)
        {
            return new SlotRevalidationResponse(false, "This address is no longer serviceable for this service.");
        }

        bool stillAvailable = availability.Value.Slots.Any(s => s.SlotWindowId == slotWindowId);
        return new SlotRevalidationResponse(
            stillAvailable,
            stillAvailable ? null : DescribeUnavailability(availability.Value.Reason));
    }

    /// <summary>
    /// Customer-facing wording for a slot that did not survive revalidation.
    /// Specific per cause: "no longer available" covers every case and
    /// explains none of them, which leaves the customer re-picking the same
    /// slot to see whether it takes this time.
    /// </summary>
    private static string DescribeUnavailability(SlotUnavailabilityReason reason) => reason switch
    {
        SlotUnavailabilityReason.FullyBooked => "This slot has just been fully booked. Please choose another.",
        SlotUnavailabilityReason.CutoffPassed => "Bookings for this slot have closed. Please choose a later slot.",
        SlotUnavailabilityReason.Blackout => "We aren't taking bookings on this date. Please choose another day.",
        SlotUnavailabilityReason.DateOutOfBookableRange => "This date is outside the window we can book. Please choose another day.",
        _ => "This slot is no longer available. Please choose another.",
    };

    public async Task<Result> ReserveSlotAsync(Guid slotWindowId, DateOnly date)
    {
        var window = await _slotWindowRepository.GetByIdAsync(slotWindowId);
        if (window is null)
        {
            return Result.Failure(Error.NotFound("Slots.WindowNotFound", "The specified slot window does not exist."));
        }

        if (window.MaxBookingsPerSlot is null)
        {
            return Result.Success();
        }

        bool reserved = await _slotCapacityRepository.TryReserveAsync(slotWindowId, date, window.MaxBookingsPerSlot.Value);
        return reserved
            ? Result.Success()
            : Result.Failure(Error.Conflict(
                "Booking.SlotCapacityReached",
                "This slot has reached its maximum bookings for the day. Please choose a different slot."));
    }

    public async Task ReleaseSlotAsync(Guid slotWindowId, DateOnly date)
    {
        var window = await _slotWindowRepository.GetByIdAsync(slotWindowId);

        // Nothing was reserved for an uncapped window, so there is nothing to
        // hand back. Guarded on the window rather than blindly decrementing so
        // a window that had its cap removed after the booking was made cannot
        // push a stale counter below the seats still legitimately held.
        if (window?.MaxBookingsPerSlot is null)
        {
            return;
        }

        await _slotCapacityRepository.ReleaseAsync(slotWindowId, date);
    }

    private static SlotOptionResponse ToOption(SlotWindow window) => new(
        window.Id,
        window.Name,
        window.StartTime,
        window.EndTime,
        window.MaxBookingsPerSlot);
}
