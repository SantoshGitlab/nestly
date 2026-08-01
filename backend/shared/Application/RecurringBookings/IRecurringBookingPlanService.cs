using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.RecurringBookings;

/// <summary>Create/pause/resume/cancel a recurring plan and read its state (task 186). Every method is scoped to the caller's own customer id, same convention as <c>IBookingService</c>.</summary>
public interface IRecurringBookingPlanService
{
    /// <summary>
    /// Validates the request through the exact same summary orchestration a
    /// one-off booking preview uses (<see cref="Bookings.IBookingSummaryService"/>,
    /// task 58) against <see cref="CreateRecurringBookingPlanRequest.StartDate"/>
    /// as the trial slot date - so a plan can never be created against a
    /// service/address/slot combination that would immediately fail its own
    /// first occurrence. Persists the plan only if that validation passes.
    /// </summary>
    Task<Result<RecurringBookingPlanResponse>> CreateAsync(Guid customerId, CreateRecurringBookingPlanRequest request);

    Task<Result<IReadOnlyList<RecurringBookingPlanResponse>>> ListAsync(Guid customerId);

    Task<Result<RecurringBookingPlanResponse>> GetAsync(Guid customerId, Guid planId);

    Task<Result<RecurringBookingPlanResponse>> PauseAsync(Guid customerId, Guid planId);

    Task<Result<RecurringBookingPlanResponse>> ResumeAsync(Guid customerId, Guid planId);

    Task<Result<RecurringBookingPlanResponse>> CancelAsync(Guid customerId, Guid planId);

    /// <summary>Projected future dates plus recent recorded outcomes, for the manage screen (task 187).</summary>
    Task<Result<IReadOnlyList<UpcomingOccurrenceResponse>>> ListUpcomingOccurrencesAsync(Guid customerId, Guid planId, int count = 5);

    Task<Result<IReadOnlyList<OccurrenceHistoryResponse>>> ListOccurrenceHistoryAsync(Guid customerId, Guid planId);
}
