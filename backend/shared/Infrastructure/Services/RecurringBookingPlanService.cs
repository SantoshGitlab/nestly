using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.RecurringBookings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Create/pause/resume/cancel a recurring booking plan and read its state (task 186).</summary>
public class RecurringBookingPlanService : IRecurringBookingPlanService
{
    private readonly IRecurringBookingPlanRepository _planRepository;
    private readonly IRecurringBookingOccurrenceRepository _occurrenceRepository;
    private readonly IBookingSummaryService _bookingSummaryService;
    private readonly IServiceRepository _serviceRepository;

    public RecurringBookingPlanService(
        IRecurringBookingPlanRepository planRepository,
        IRecurringBookingOccurrenceRepository occurrenceRepository,
        IBookingSummaryService bookingSummaryService,
        IServiceRepository serviceRepository)
    {
        _planRepository = planRepository;
        _occurrenceRepository = occurrenceRepository;
        _bookingSummaryService = bookingSummaryService;
        _serviceRepository = serviceRepository;
    }

    public async Task<Result<RecurringBookingPlanResponse>> CreateAsync(Guid customerId, CreateRecurringBookingPlanRequest request)
    {
        // Dry-runs the plan's first occurrence through the exact same
        // orchestration a one-off booking preview uses (task 58) - identity,
        // active catalog, serviceable address, valid slot, price snapshot -
        // so a plan can never be created against a combination that would
        // just fail-and-notify on its very first attempt. Nothing here
        // persists a booking; GetSummaryAsync only validates and prices.
        var summaryRequest = new BookingSummaryRequest(
            request.ServiceId, request.CityId, request.AddressId, request.LocalityId,
            request.SlotWindowId, request.StartDate, request.Quantity, request.AddOns,
            ApplyWalletCredit: request.ApplyWalletCredit);

        var summaryResult = await _bookingSummaryService.GetSummaryAsync(customerId, summaryRequest);
        if (summaryResult.IsFailure)
        {
            return summaryResult.Error;
        }

        RecurringBookingPlan plan;
        try
        {
            plan = new RecurringBookingPlan(
                Guid.NewGuid(), customerId, request.ServiceId, request.CityId, request.LocalityId,
                request.AddressId, request.SlotWindowId, request.Quantity, request.Frequency,
                request.RecurrenceDayOfWeek, request.RecurrenceDayOfMonth, request.StartDate,
                request.EndDate, request.OccurrenceCount,
                request.AddOns.Select(a => (a.AddOnId, a.Quantity)).ToList(),
                request.ApplyWalletCredit);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("RecurringBookingPlan.InvalidRequest", ex.Message);
        }

        await _planRepository.AddAsync(plan);

        return ToResponse(plan, summaryResult.Value.Service.Name);
    }

    public async Task<Result<IReadOnlyList<RecurringBookingPlanResponse>>> ListAsync(Guid customerId)
    {
        var plans = await _planRepository.ListByCustomerAsync(customerId);
        var responses = new List<RecurringBookingPlanResponse>(plans.Count);
        foreach (var plan in plans)
        {
            responses.Add(ToResponse(plan, await ResolveServiceNameAsync(plan.ServiceId)));
        }

        return Result.Success<IReadOnlyList<RecurringBookingPlanResponse>>(responses);
    }

    public async Task<Result<RecurringBookingPlanResponse>> GetAsync(Guid customerId, Guid planId)
    {
        var planResult = await ResolveOwnedPlanAsync(customerId, planId);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        var plan = planResult.Value;
        return ToResponse(plan, await ResolveServiceNameAsync(plan.ServiceId));
    }

    public async Task<Result<RecurringBookingPlanResponse>> PauseAsync(Guid customerId, Guid planId) =>
        await TransitionAsync(customerId, planId, plan => plan.Pause(), "RecurringBookingPlan.InvalidPause");

    public async Task<Result<RecurringBookingPlanResponse>> ResumeAsync(Guid customerId, Guid planId) =>
        await TransitionAsync(customerId, planId, plan => plan.Resume(), "RecurringBookingPlan.InvalidResume");

    public async Task<Result<RecurringBookingPlanResponse>> CancelAsync(Guid customerId, Guid planId) =>
        await TransitionAsync(customerId, planId, plan => plan.Cancel(), "RecurringBookingPlan.InvalidCancel");

    public async Task<Result<IReadOnlyList<UpcomingOccurrenceResponse>>> ListUpcomingOccurrencesAsync(Guid customerId, Guid planId, int count = 5)
    {
        var planResult = await ResolveOwnedPlanAsync(customerId, planId);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        IReadOnlyList<UpcomingOccurrenceResponse> response = planResult.Value
            .PreviewUpcomingOccurrenceDates(count)
            .Select(date => new UpcomingOccurrenceResponse(date, IsProjected: true))
            .ToList();

        return Result.Success(response);
    }

    public async Task<Result<IReadOnlyList<OccurrenceHistoryResponse>>> ListOccurrenceHistoryAsync(Guid customerId, Guid planId)
    {
        var planResult = await ResolveOwnedPlanAsync(customerId, planId);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        var occurrences = await _occurrenceRepository.ListByPlanAsync(planId);
        IReadOnlyList<OccurrenceHistoryResponse> response = occurrences
            .OrderByDescending(o => o.ScheduledDate)
            .Select(o => new OccurrenceHistoryResponse(o.ScheduledDate, o.Outcome, o.BookingId, o.SkipReason, o.ProcessedAtUtc))
            .ToList();

        return Result.Success(response);
    }

    private async Task<Result<RecurringBookingPlanResponse>> TransitionAsync(
        Guid customerId, Guid planId, Action<RecurringBookingPlan> transition, string errorCode)
    {
        var planResult = await ResolveOwnedPlanAsync(customerId, planId);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        var plan = planResult.Value;
        try
        {
            transition(plan);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business(errorCode, ex.Message);
        }

        await _planRepository.UpdateAsync(plan);

        return ToResponse(plan, await ResolveServiceNameAsync(plan.ServiceId));
    }

    private async Task<Result<RecurringBookingPlan>> ResolveOwnedPlanAsync(Guid customerId, Guid planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan is null || plan.CustomerId != customerId)
        {
            return Error.NotFound("RecurringBookingPlan.NotFound", "The specified recurring booking plan does not exist.");
        }

        return plan;
    }

    private async Task<string> ResolveServiceNameAsync(Guid serviceId) =>
        (await _serviceRepository.GetByIdAsync(serviceId))?.Name ?? string.Empty;

    private static RecurringBookingPlanResponse ToResponse(RecurringBookingPlan plan, string serviceName) => new(
        plan.Id, plan.ServiceId, serviceName, plan.AddressId, plan.SlotWindowId, plan.Quantity, plan.ApplyWalletCredit,
        plan.Frequency, plan.RecurrenceDayOfWeek, plan.RecurrenceDayOfMonth, plan.StartDate, plan.EndDate,
        plan.OccurrenceCount, plan.CompletedOccurrenceCount, plan.NextOccurrenceDate, plan.Status, plan.CreatedAtUtc);
}
