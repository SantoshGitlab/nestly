using Nestly.Application;
using Nestly.Application.Serviceability;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Slot availability calculation (tasks 45a-d, SRS 12.10). Single-timezone
/// assumption throughout (server clock = business clock) - no per-city
/// timezone field exists anywhere in the geography schema; add one if the
/// platform ever spans multiple timezones.
/// </summary>
public class SlotAvailabilityService : ISlotAvailabilityService
{
    private readonly IServiceabilityRepository _serviceabilityRepository;
    private readonly IServiceabilityValidationService _serviceabilityValidationService;
    private readonly ISlotWindowRepository _slotWindowRepository;
    private readonly ISlotBlackoutRepository _blackoutRepository;
    private readonly ISlotBookingPolicyRepository _policyRepository;
    private readonly ISlotCapacityRepository _slotCapacityRepository;
    private readonly TimeProvider _timeProvider;

    public SlotAvailabilityService(
        IServiceabilityRepository serviceabilityRepository,
        IServiceabilityValidationService serviceabilityValidationService,
        ISlotWindowRepository slotWindowRepository,
        ISlotBlackoutRepository blackoutRepository,
        ISlotBookingPolicyRepository policyRepository,
        ISlotCapacityRepository slotCapacityRepository,
        TimeProvider timeProvider)
    {
        _serviceabilityRepository = serviceabilityRepository;
        _serviceabilityValidationService = serviceabilityValidationService;
        _slotWindowRepository = slotWindowRepository;
        _blackoutRepository = blackoutRepository;
        _policyRepository = policyRepository;
        _slotCapacityRepository = slotCapacityRepository;
        _timeProvider = timeProvider;
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
            return new SlotAvailabilityResponse(IsServiceable: false, Slots: []);
        }

        DateTime now = _timeProvider.GetUtcNow().DateTime;
        DateOnly today = DateOnly.FromDateTime(now);

        var policy = await _policyRepository.GetByCityAsync(cityId);
        int maxAdvanceDays = policy?.MaxAdvanceDays ?? int.MaxValue;
        int cutoffMinutes = policy?.CutoffMinutes ?? 0;

        if (date < today || (maxAdvanceDays != int.MaxValue && date > today.AddDays(maxAdvanceDays)))
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: []);
        }

        var blackouts = await _blackoutRepository.ListInRangeAsync(cityId, date, date);
        if (blackouts.Any(b => b.CoversDate(date)))
        {
            return new SlotAvailabilityResponse(IsServiceable: true, Slots: []);
        }

        var windows = await _slotWindowRepository.ListActiveForCityAndDayAsync(cityId, date.DayOfWeek);
        DateTime cutoffThreshold = now.AddMinutes(cutoffMinutes);

        var slots = windows
            .Where(w => date.ToDateTime(TimeOnly.MinValue).Add(w.StartTime) >= cutoffThreshold)
            .Select(ToOption)
            .ToList();

        return new SlotAvailabilityResponse(IsServiceable: true, Slots: slots);
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
            stillAvailable ? null : "This slot is no longer available (cutoff passed, blacked out, or out of the bookable range).");
    }

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

    private static SlotOptionResponse ToOption(SlotWindow window) => new(
        window.Id,
        window.Name,
        window.StartTime,
        window.EndTime,
        window.MaxBookingsPerSlot);
}
