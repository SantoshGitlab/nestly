using Nestly.Application;
using Nestly.Application.Serviceability;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin CRUD over slot configuration (SRS 12.10, tasks 113a-e): recurring
/// windows and their day-of-week rules, holiday/blackout dates, per-city
/// booking cutoffs and advance-booking limits, per-window capacity, and
/// one-off availability overrides.
/// </summary>
public class SlotManagementService : ISlotManagementService
{
    private readonly ISlotWindowRepository _slotWindowRepository;
    private readonly ISlotBlackoutRepository _slotBlackoutRepository;
    private readonly ISlotBookingPolicyRepository _slotBookingPolicyRepository;
    private readonly ISlotAvailabilityOverrideRepository _slotAvailabilityOverrideRepository;
    private readonly ICityRepository _cityRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;

    public SlotManagementService(
        ISlotWindowRepository slotWindowRepository,
        ISlotBlackoutRepository slotBlackoutRepository,
        ISlotBookingPolicyRepository slotBookingPolicyRepository,
        ISlotAvailabilityOverrideRepository slotAvailabilityOverrideRepository,
        ICityRepository cityRepository,
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository)
    {
        _slotWindowRepository = slotWindowRepository;
        _slotBlackoutRepository = slotBlackoutRepository;
        _slotBookingPolicyRepository = slotBookingPolicyRepository;
        _slotAvailabilityOverrideRepository = slotAvailabilityOverrideRepository;
        _cityRepository = cityRepository;
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
    }

    // ---- Lookups ----

    public async Task<IReadOnlyList<SlotCityLookupResponse>> ListCityLookupsAsync()
    {
        var cities = await _cityRepository.ListAsync(null);
        return cities.Where(c => c.IsActive).Select(c => new SlotCityLookupResponse(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<CategoryLookupResponse>> ListCategoryLookupsAsync()
    {
        // Empty query matches every active category (SearchActiveAsync's
        // Contains("") is always true) - the same "list all" trick
        // ServiceabilityMappingManagementService uses for its own lookups.
        var categories = await _categoryRepository.SearchActiveAsync(string.Empty);
        return categories.Select(c => new CategoryLookupResponse(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<ServiceLookupResponse>> ListServiceLookupsAsync()
    {
        var services = await _serviceRepository.SearchActiveAsync(string.Empty);
        return services.Select(s => new ServiceLookupResponse(s.Id, s.Name)).ToList();
    }

    // ---- Windows (task 113a) ----

    public async Task<IReadOnlyList<SlotWindowAdminResponse>> ListWindowsAsync(Guid? cityId)
    {
        var windows = await _slotWindowRepository.ListAsync(cityId);

        // Task 256: rule days were fetched a window at a time.
        var ruleDays = await _slotWindowRepository.ListRuleDaysByWindowIdsAsync(
            windows.Select(w => w.Id).ToList());

        return windows
            .Select(window => ToResponse(window, ruleDays.GetValueOrDefault(window.Id, [])))
            .ToList();
    }

    public async Task<Result<SlotWindowAdminResponse>> CreateWindowAsync(SlotWindowCreateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Slots.CityNotFound", "The specified city does not exist.");
        }

        var window = new SlotWindow(Guid.NewGuid(), request.CityId, request.Name, request.StartTime, request.EndTime);
        window.SetCapacity(request.MaxBookingsPerSlot);

        await _slotWindowRepository.AddAsync(window);
        await _slotWindowRepository.ReplaceRulesAsync(window.Id, request.DaysOfWeek);

        return ToResponse(window, request.DaysOfWeek, city.Name);
    }

    public async Task<Result<SlotWindowAdminResponse>> UpdateWindowAsync(Guid id, SlotWindowUpdateRequest request)
    {
        var window = await _slotWindowRepository.GetByIdAsync(id);
        if (window is null)
        {
            return Error.NotFound("Slots.WindowNotFound", "The specified slot window does not exist.");
        }

        window.SetName(request.Name);
        window.SetTimes(request.StartTime, request.EndTime);
        await _slotWindowRepository.UpdateAsync(window);
        await _slotWindowRepository.ReplaceRulesAsync(window.Id, request.DaysOfWeek);

        var city = await _cityRepository.GetByIdAsync(window.CityId);
        return ToResponse(window, request.DaysOfWeek, city?.Name ?? string.Empty);
    }

    public async Task<Result<SlotWindowAdminResponse>> SetWindowCapacityAsync(Guid id, SlotWindowCapacityUpdateRequest request)
    {
        var window = await _slotWindowRepository.GetByIdAsync(id);
        if (window is null)
        {
            return Error.NotFound("Slots.WindowNotFound", "The specified slot window does not exist.");
        }

        window.SetCapacity(request.MaxBookingsPerSlot);
        await _slotWindowRepository.UpdateAsync(window);

        var days = await _slotWindowRepository.ListRuleDaysAsync(window.Id);
        var city = await _cityRepository.GetByIdAsync(window.CityId);
        return ToResponse(window, days, city?.Name ?? string.Empty);
    }

    public async Task<Result> SetWindowActiveAsync(Guid id, bool isActive)
    {
        var window = await _slotWindowRepository.GetByIdAsync(id);
        if (window is null)
        {
            return Result.Failure(Error.NotFound("Slots.WindowNotFound", "The specified slot window does not exist."));
        }

        if (isActive) window.Activate(); else window.Deactivate();
        await _slotWindowRepository.UpdateAsync(window);
        return Result.Success();
    }

    // ---- Blackouts (task 113b) ----

    public async Task<IReadOnlyList<SlotBlackoutAdminResponse>> ListBlackoutsAsync(Guid? cityId)
    {
        var blackouts = await _slotBlackoutRepository.ListAsync(cityId);

        // Task 256: one city lookup per blackout row.
        var cityNames = await _cityRepository.GetNamesByIdsAsync(
            blackouts.Select(b => b.CityId).Distinct().ToList());

        return blackouts
            .Select(blackout => new SlotBlackoutAdminResponse(
                blackout.Id, blackout.CityId, cityNames.GetValueOrDefault(blackout.CityId, string.Empty),
                blackout.StartDate, blackout.EndDate, blackout.Type, blackout.Reason))
            .ToList();
    }

    public async Task<Result<SlotBlackoutAdminResponse>> CreateBlackoutAsync(SlotBlackoutCreateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Slots.CityNotFound", "The specified city does not exist.");
        }

        var blackout = new SlotBlackout(Guid.NewGuid(), request.CityId, request.StartDate, request.EndDate, request.Type, request.Reason);
        await _slotBlackoutRepository.AddAsync(blackout);

        return new SlotBlackoutAdminResponse(
            blackout.Id, blackout.CityId, city.Name, blackout.StartDate, blackout.EndDate, blackout.Type, blackout.Reason);
    }

    public async Task<Result> DeleteBlackoutAsync(Guid id)
    {
        var blackout = await _slotBlackoutRepository.GetByIdAsync(id);
        if (blackout is null)
        {
            return Result.Failure(Error.NotFound("Slots.BlackoutNotFound", "The specified blackout does not exist."));
        }

        await _slotBlackoutRepository.DeleteAsync(blackout);
        return Result.Success();
    }

    // ---- Cutoffs / advance-booking policy (task 113c) ----

    public async Task<IReadOnlyList<SlotBookingPolicyAdminResponse>> ListBookingPoliciesAsync()
    {
        var policies = await _slotBookingPolicyRepository.ListAsync();

        // Task 256: one city lookup per policy row.
        var cityNames = await _cityRepository.GetNamesByIdsAsync(
            policies.Select(p => p.CityId).Distinct().ToList());

        return policies
            .Select(policy => new SlotBookingPolicyAdminResponse(
                policy.Id, policy.CityId, cityNames.GetValueOrDefault(policy.CityId, string.Empty),
                policy.CutoffMinutes, policy.MaxAdvanceDays))
            .OrderBy(r => r.CityName)
            .ToList();
    }

    public async Task<Result<SlotBookingPolicyAdminResponse>> UpsertBookingPolicyAsync(SlotBookingPolicyUpsertRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Slots.CityNotFound", "The specified city does not exist.");
        }

        var policy = await _slotBookingPolicyRepository.GetByCityAsync(request.CityId);
        if (policy is null)
        {
            policy = new SlotBookingPolicy(Guid.NewGuid(), request.CityId, request.CutoffMinutes, request.MaxAdvanceDays);
            await _slotBookingPolicyRepository.AddAsync(policy);
        }
        else
        {
            policy.SetCutoffMinutes(request.CutoffMinutes);
            policy.SetMaxAdvanceDays(request.MaxAdvanceDays);
            await _slotBookingPolicyRepository.UpdateAsync(policy);
        }

        return new SlotBookingPolicyAdminResponse(policy.Id, policy.CityId, city.Name, policy.CutoffMinutes, policy.MaxAdvanceDays);
    }

    // ---- Availability overrides (task 113e, SRS 12.10.2) ----

    public async Task<IReadOnlyList<SlotAvailabilityOverrideAdminResponse>> ListOverridesAsync(Guid? cityId, DateOnly? date)
    {
        var overrides = await _slotAvailabilityOverrideRepository.ListAsync(cityId, date);

        // Task 256: four name lookups per row become four for the whole page.
        var names = await LoadOverrideNamesAsync(overrides);

        return overrides.Select(o => ToResponse(o, names)).ToList();
    }

    public async Task<Result<SlotAvailabilityOverrideAdminResponse>> CreateOverrideAsync(SlotAvailabilityOverrideCreateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Slots.CityNotFound", "The specified city does not exist.");
        }

        if (request.SlotWindowId is not null && await _slotWindowRepository.GetByIdAsync(request.SlotWindowId.Value) is null)
        {
            return Error.NotFound("Slots.WindowNotFound", "The specified slot window does not exist.");
        }

        if (request.CategoryId is not null && await _categoryRepository.GetByIdAsync(request.CategoryId.Value) is null)
        {
            return Error.NotFound("Slots.CategoryNotFound", "The specified category does not exist.");
        }

        if (request.ServiceId is not null && await _serviceRepository.GetByIdAsync(request.ServiceId.Value) is null)
        {
            return Error.NotFound("Slots.ServiceNotFound", "The specified service does not exist.");
        }

        var overrideRecord = new SlotAvailabilityOverride(
            Guid.NewGuid(), request.CityId, request.Date, request.Reason,
            request.SlotWindowId, request.CategoryId, request.ServiceId);
        await _slotAvailabilityOverrideRepository.AddAsync(overrideRecord);

        return await ToResponseAsync(overrideRecord);
    }

    public async Task<Result> DeleteOverrideAsync(Guid id)
    {
        var overrideRecord = await _slotAvailabilityOverrideRepository.GetByIdAsync(id);
        if (overrideRecord is null)
        {
            return Result.Failure(Error.NotFound("Slots.OverrideNotFound", "The specified override does not exist."));
        }

        await _slotAvailabilityOverrideRepository.DeleteAsync(overrideRecord);
        return Result.Success();
    }

    /// <summary>
    /// The four name lookups an override row renders, resolved for a whole
    /// batch at once (task 256). Rendering one override used to cost up to
    /// four queries, and <see cref="ListOverridesAsync"/> paid that per row.
    /// </summary>
    private sealed record OverrideNames(
        IReadOnlyDictionary<Guid, string> Cities,
        IReadOnlyDictionary<Guid, string> Windows,
        IReadOnlyDictionary<Guid, string> Categories,
        IReadOnlyDictionary<Guid, string> Services);

    private async Task<OverrideNames> LoadOverrideNamesAsync(IReadOnlyList<SlotAvailabilityOverride> records) =>
        new(
            await _cityRepository.GetNamesByIdsAsync(
                records.Select(r => r.CityId).Distinct().ToList()),
            await _slotWindowRepository.GetNamesByIdsAsync(
                records.Where(r => r.SlotWindowId is not null).Select(r => r.SlotWindowId!.Value).Distinct().ToList()),
            await _categoryRepository.GetNamesByIdsAsync(
                records.Where(r => r.CategoryId is not null).Select(r => r.CategoryId!.Value).Distinct().ToList()),
            await _serviceRepository.GetNamesByIdsAsync(
                records.Where(r => r.ServiceId is not null).Select(r => r.ServiceId!.Value).Distinct().ToList()));

    private static SlotAvailabilityOverrideAdminResponse ToResponse(SlotAvailabilityOverride overrideRecord, OverrideNames names) =>
        new(
            overrideRecord.Id,
            overrideRecord.CityId,
            names.Cities.GetValueOrDefault(overrideRecord.CityId, string.Empty),
            overrideRecord.Date,
            overrideRecord.SlotWindowId,
            overrideRecord.SlotWindowId is null ? null : names.Windows.GetValueOrDefault(overrideRecord.SlotWindowId.Value),
            overrideRecord.CategoryId,
            overrideRecord.CategoryId is null ? null : names.Categories.GetValueOrDefault(overrideRecord.CategoryId.Value),
            overrideRecord.ServiceId,
            overrideRecord.ServiceId is null ? null : names.Services.GetValueOrDefault(overrideRecord.ServiceId.Value),
            overrideRecord.Reason);

    private async Task<SlotAvailabilityOverrideAdminResponse> ToResponseAsync(SlotAvailabilityOverride overrideRecord) =>
        ToResponse(overrideRecord, await LoadOverrideNamesAsync([overrideRecord]));

    private static SlotWindowAdminResponse ToResponse(SlotWindow window, IReadOnlyList<DayOfWeek> daysOfWeek, string? cityName = null) =>
        new(window.Id, window.CityId, cityName ?? window.City?.Name ?? string.Empty, window.Name,
            window.StartTime, window.EndTime, window.IsActive, window.MaxBookingsPerSlot, daysOfWeek);
}
