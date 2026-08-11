using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.RecurringBookings;

/// <summary>
/// The admin-side read surface over <c>recurring_booking_plan</c> (task 299):
/// a filterable list of every plan on the platform, plus the status/cadence/
/// volume report behind it.
///
/// Read-only by design, and separate from <see cref="IRecurringBookingPlanService"/>
/// rather than an admin overload on it: that service is entirely
/// customer-scoped (every method takes the caller's customer id and refuses
/// anything it does not own), and widening it to sometimes skip that check
/// would put the ownership guard behind a boolean. Admin pause/resume/cancel
/// of someone else's standing instruction is not in this task's scope and is
/// deliberately not offered here - an admin who needs to stop the work a plan
/// generates cancels the individual bookings through
/// <c>IBookingManagementService</c>, which already audits.
/// </summary>
public interface IRecurringBookingPlanAdminService
{
    /// <summary>Horizon applied when the caller supplies no date range - four weeks, long enough to contain at least one occurrence of every supported cadence except monthly's worst case.</summary>
    const int DefaultHorizonDays = 28;

    Task<Result<AdminRecurringPlanSearchResponse>> SearchAsync(AdminRecurringPlanSearchRequest request);

    /// <summary>Validation failure when <c>ToDate</c> precedes <c>FromDate</c>, matching <c>IReportingQueryService</c>'s "Reports.InvalidDateRange".</summary>
    Task<Result<AdminRecurringPlanReportResponse>> GetReportAsync(AdminRecurringPlanReportRequest request);
}
