using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Slots;

/// <summary>
/// Admin CRUD over slot configuration (SRS 12.10, tasks 113a-e): recurring
/// windows and their day-of-week rules, holiday/blackout dates, per-city
/// booking cutoffs and advance-booking limits, per-window capacity, and
/// one-off availability overrides.
/// </summary>
public interface ISlotManagementService
{
    // ---- Lookups ----
    Task<IReadOnlyList<SlotCityLookupResponse>> ListCityLookupsAsync();
    Task<IReadOnlyList<CategoryLookupResponse>> ListCategoryLookupsAsync();
    Task<IReadOnlyList<ServiceLookupResponse>> ListServiceLookupsAsync();

    Task<IReadOnlyList<SlotWindowAdminResponse>> ListWindowsAsync(Guid? cityId);
    Task<Result<SlotWindowAdminResponse>> CreateWindowAsync(SlotWindowCreateRequest request);
    Task<Result<SlotWindowAdminResponse>> UpdateWindowAsync(Guid id, SlotWindowUpdateRequest request);
    Task<Result<SlotWindowAdminResponse>> SetWindowCapacityAsync(Guid id, SlotWindowCapacityUpdateRequest request);
    Task<Result> SetWindowActiveAsync(Guid id, bool isActive);

    Task<IReadOnlyList<SlotBlackoutAdminResponse>> ListBlackoutsAsync(Guid? cityId);
    Task<Result<SlotBlackoutAdminResponse>> CreateBlackoutAsync(SlotBlackoutCreateRequest request);
    Task<Result> DeleteBlackoutAsync(Guid id);

    Task<IReadOnlyList<SlotBookingPolicyAdminResponse>> ListBookingPoliciesAsync();
    Task<Result<SlotBookingPolicyAdminResponse>> UpsertBookingPolicyAsync(SlotBookingPolicyUpsertRequest request);

    Task<IReadOnlyList<SlotAvailabilityOverrideAdminResponse>> ListOverridesAsync(Guid? cityId, DateOnly? date);
    Task<Result<SlotAvailabilityOverrideAdminResponse>> CreateOverrideAsync(SlotAvailabilityOverrideCreateRequest request);
    Task<Result> DeleteOverrideAsync(Guid id);
}
