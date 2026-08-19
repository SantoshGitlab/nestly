using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderManagement;

/// <summary>
/// One booking caught in a conflict group (task 321). Carries enough to act on
/// without a second round-trip per row: who the customer is, what was booked,
/// the exact slot window, and the live assignment's own state - an
/// <c>Accepted</c> assignment is a commitment the provider has already made,
/// an <c>Assigned</c> one is only an outstanding offer, and an admin choosing
/// which of two clashing jobs to move needs that distinction in front of them.
/// </summary>
public sealed record ConflictedBookingSummary(
    Guid BookingId,
    Guid AssignmentId,
    Nestly.Domain.BookingProviderAssignmentStatus AssignmentStatus,
    Nestly.Domain.BookingStatus BookingStatus,
    Nestly.Domain.BookingAssignedByType AssignedByType,
    DateTime AssignedAt,
    string CustomerName,
    string ServiceName,
    DateOnly SlotDate,
    TimeSpan StartTime,
    TimeSpan EndTime);

/// <summary>
/// A set of two or more bookings that one provider is live on at overlapping
/// times (task 321). The group is the unit an admin resolves: moving a single
/// booking out of it is what clears the clash, so the UI needs the whole set
/// together rather than a flat list of offending bookings.
/// </summary>
/// <param name="WindowStart">
/// The earliest start and latest end across the group's bookings - the span
/// the clash lives inside, not a slot any one booking occupies. Presented so
/// an admin can see the shape of the collision at a glance.
/// </param>
public sealed record BookingAssignmentConflictGroup(
    Guid ProviderId,
    string ProviderDisplayName,
    string ProviderPhone,
    DateOnly SlotDate,
    TimeSpan WindowStart,
    TimeSpan WindowEnd,
    IReadOnlyList<ConflictedBookingSummary> Bookings);

/// <summary>Paged conflict groups plus the total, so the dashboard can page without recomputing the count.</summary>
public sealed record BookingAssignmentConflictSearchResponse(
    IReadOnlyList<BookingAssignmentConflictGroup> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Task 321: "which bookings are double-booked on one provider right now?"
///
/// <para>
/// This is the detection half of the invariant
/// <see cref="IProviderScheduleConflictService"/> enforces. That service is
/// asked one question at a time - "may this provider take this booking?" - on
/// the way in, and the <c>ex_booking_provider_no_double_booking</c> exclusion
/// constraint backstops it at the database. Neither can answer the question
/// this service exists for, because both are gates rather than reports: a row
/// written before task 288 shipped, one that slipped through a lost race, or
/// one created by any future writer that bypasses the service, is a standing
/// conflict that nothing currently surfaces. An invariant with no way to
/// observe its own violations is only half an invariant.
/// </para>
///
/// <para>
/// Overlap semantics are deliberately identical to
/// <see cref="IProviderScheduleConflictService.FindConflictAsync"/>'s -
/// half-open <c>[start, end)</c>, live meaning <c>Assigned</c>/<c>Accepted</c>
/// only. Detection and prevention must not be able to disagree about what a
/// conflict is; if they did, this dashboard would either nag about bookings
/// the assignment path considers legal, or stay silent about ones it would
/// have rejected.
/// </para>
///
/// <para>Read-only. Resolution runs through the existing
/// <see cref="IBookingProviderAssignmentService.AssignAsync"/>, so a
/// reassignment made from the conflicts dashboard is validated, audited and
/// conflict-checked exactly like any other manual assignment.</para>
/// </summary>
public interface IBookingAssignmentConflictService
{
    /// <summary>
    /// Conflict groups, earliest slot first. <paramref name="fromDate"/>
    /// defaults to today: a clash in the past cannot be resolved by moving
    /// anyone, so the dashboard's default view is the work an admin can still
    /// act on. Pass an explicit earlier date to audit historical damage.
    /// </summary>
    Task<Result<BookingAssignmentConflictSearchResponse>> SearchAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many conflict groups exist from <paramref name="fromDate"/> (default
    /// today) onward - the dashboard's badge count, kept separate so the nav
    /// can show it without paying for a page of detail.
    /// </summary>
    Task<Result<int>> CountAsync(DateOnly? fromDate, CancellationToken cancellationToken = default);
}
