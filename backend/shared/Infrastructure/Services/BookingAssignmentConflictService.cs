using Microsoft.EntityFrameworkCore;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IBookingAssignmentConflictService"/>.</summary>
public class BookingAssignmentConflictService : IBookingAssignmentConflictService
{
    /// <summary>
    /// Upper bound on live assignments pulled into memory for one query
    /// window. Grouping overlapping intervals is not expressible in the
    /// provider-agnostic LINQ this codebase targets (see the in-memory note
    /// below), so the work happens in process and must not be unbounded: a
    /// pathological date range would otherwise stream the whole assignment
    /// table. Conflicts are by nature rare, so a window that genuinely
    /// contains more live jobs than this is a range too wide to be a useful
    /// dashboard view, and the caller is told rather than silently served a
    /// truncated answer.
    /// </summary>
    private const int MaxScannedAssignments = 20_000;

    private const int MaxPageSize = 100;

    // Same reasoning as ProviderScheduleConflictService: the question spans
    // BookingProviderAssignment (who is committed), Booking (when the slot is)
    // and Provider (who to show the admin), and no single repository owns that
    // join.
    private readonly NestlyDbContext _context;

    public BookingAssignmentConflictService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BookingAssignmentConflictSearchResponse>> SearchAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return Result.Failure<BookingAssignmentConflictSearchResponse>(
                Error.Validation("BookingConflicts.InvalidPage", "Page must be 1 or greater."));
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<BookingAssignmentConflictSearchResponse>(
                Error.Validation("BookingConflicts.InvalidPageSize", $"Page size must be between 1 and {MaxPageSize}."));
        }

        DateOnly from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (toDate is { } to && to < from)
        {
            return Result.Failure<BookingAssignmentConflictSearchResponse>(
                Error.Validation("BookingConflicts.InvalidRange", "toDate cannot be earlier than fromDate."));
        }

        var groupsResult = await BuildGroupsAsync(from, toDate, cancellationToken);
        if (groupsResult.IsFailure)
        {
            return Result.Failure<BookingAssignmentConflictSearchResponse>(groupsResult.Error);
        }

        var groups = groupsResult.Value;

        var items = groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(new BookingAssignmentConflictSearchResponse(items, groups.Count, page, pageSize));
    }

    public async Task<Result<int>> CountAsync(DateOnly? fromDate, CancellationToken cancellationToken = default)
    {
        DateOnly from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var groupsResult = await BuildGroupsAsync(from, toDate: null, cancellationToken);

        return groupsResult.IsFailure
            ? Result.Failure<int>(groupsResult.Error)
            : Result.Success(groupsResult.Value.Count);
    }

    private async Task<Result<List<BookingAssignmentConflictGroup>>> BuildGroupsAsync(
        DateOnly fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        // "Live" mirrors BookingProviderAssignmentStatus's doc comment and
        // IProviderScheduleConflictService's own filter exactly - a
        // Rejected/Reassigned/Withdrawn row is nobody's commitment any more,
        // so it can neither cause nor join a conflict.
        var liveStatuses = new[] { BookingProviderAssignmentStatus.Assigned, BookingProviderAssignmentStatus.Accepted };

        var query = _context.Set<BookingProviderAssignment>()
            .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
            .Where(x => liveStatuses.Contains(x.Assignment.Status) && x.Booking.SlotDate >= fromDate);

        if (toDate is { } to)
        {
            query = query.Where(x => x.Booking.SlotDate <= to);
        }

        // Narrowed in SQL to live assignments inside the window, then compared
        // as intervals in memory: TimeSpan comparisons do not reliably
        // translate on the SQLite provider the test suite runs against, which
        // is the same reason ProviderScheduleConflictService and
        // ProviderAssignmentEligibilityService both compare in memory.
        var rows = await query
            .OrderBy(x => x.Booking.SlotDate)
            .ThenBy(x => x.Booking.SlotStartTimeSnapshot)
            .Select(x => new ScannedAssignment(
                x.Assignment.Id,
                x.Assignment.ProviderId,
                x.Assignment.Status,
                x.Assignment.AssignedByType,
                x.Assignment.AssignedAt,
                x.Booking.Id,
                x.Booking.Status,
                x.Booking.CustomerNameSnapshot,
                x.Booking.SlotDate,
                x.Booking.SlotStartTimeSnapshot,
                x.Booking.SlotEndTimeSnapshot,
                // FirstOrDefault over a projected column rather than
                // Items[0].NameSnapshot: list indexing does not translate, and
                // an untranslatable projection would silently become
                // client-side evaluation or throw. Null (no items) is
                // normalised in ToSummary, matching BookingManagementService's
                // own "(no service)" fallback.
                x.Booking.Items.Select(i => i.NameSnapshot).FirstOrDefault()))
            .Take(MaxScannedAssignments + 1)
            .ToListAsync(cancellationToken);

        if (rows.Count > MaxScannedAssignments)
        {
            return Result.Failure<List<BookingAssignmentConflictGroup>>(Error.Validation(
                "BookingConflicts.RangeTooWide",
                $"More than {MaxScannedAssignments:N0} live assignments fall in this date range. Narrow the range and try again."));
        }

        var providerNames = await LoadProviderLabelsAsync(rows, cancellationToken);

        var groups = new List<BookingAssignmentConflictGroup>();

        foreach (var perProviderDay in rows.GroupBy(r => new { r.ProviderId, r.SlotDate }))
        {
            foreach (var cluster in BuildOverlapClusters(perProviderDay.OrderBy(r => r.StartTime).ThenBy(r => r.EndTime).ToList()))
            {
                var label = providerNames.GetValueOrDefault(
                    perProviderDay.Key.ProviderId,
                    new ProviderLabel("(unknown provider)", string.Empty));

                groups.Add(new BookingAssignmentConflictGroup(
                    perProviderDay.Key.ProviderId,
                    label.DisplayName,
                    label.Phone,
                    perProviderDay.Key.SlotDate,
                    cluster.Min(r => r.StartTime),
                    cluster.Max(r => r.EndTime),
                    cluster.Select(ToSummary).ToList()));
            }
        }

        // Soonest clash first - the one an admin has least time to fix.
        return Result.Success(groups
            .OrderBy(g => g.SlotDate)
            .ThenBy(g => g.WindowStart)
            .ThenBy(g => g.ProviderDisplayName)
            .ToList());
    }

    /// <summary>
    /// Splits one provider-day's live jobs into maximal runs of mutually
    /// overlapping bookings, keeping only runs of two or more.
    ///
    /// <para>
    /// The input must be ordered by start time. Walking it once and extending
    /// the current run while the next job starts before the run's furthest end
    /// is the standard interval-merge: a job that starts at or after that
    /// point cannot overlap anything already in the run, because every member
    /// ends no later than it. Tracking the furthest end rather than the
    /// previous job's end matters when a long job contains several short ones.
    /// </para>
    ///
    /// <para>
    /// Overlap is half-open <c>[start, end)</c> - <c>next.Start &lt; runEnd</c>,
    /// not <c>&lt;=</c> - so back-to-back 09:00-11:00 and 11:00-13:00 jobs are
    /// not a conflict, matching
    /// <see cref="IProviderScheduleConflictService.FindConflictAsync"/> and the
    /// <c>ex_booking_provider_no_double_booking</c> constraint.
    /// </para>
    /// </summary>
    private static IEnumerable<List<ScannedAssignment>> BuildOverlapClusters(IReadOnlyList<ScannedAssignment> ordered)
    {
        var run = new List<ScannedAssignment>();
        TimeSpan runEnd = TimeSpan.MinValue;

        foreach (var row in ordered)
        {
            if (run.Count > 0 && row.StartTime < runEnd)
            {
                run.Add(row);
                if (row.EndTime > runEnd)
                {
                    runEnd = row.EndTime;
                }

                continue;
            }

            if (run.Count > 1)
            {
                yield return run;
            }

            run = new List<ScannedAssignment> { row };
            runEnd = row.EndTime;
        }

        if (run.Count > 1)
        {
            yield return run;
        }
    }

    private async Task<Dictionary<Guid, ProviderLabel>> LoadProviderLabelsAsync(
        IReadOnlyCollection<ScannedAssignment> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            // `new()`, not a `[]` collection expression: C# 12 collection
            // expressions do not target Dictionary<,>, unlike the list returns
            // elsewhere in this codebase.
            return new Dictionary<Guid, ProviderLabel>();
        }

        var providerIds = rows.Select(r => r.ProviderId).Distinct().ToList();

        return await _context.Set<Provider>()
            .Where(p => providerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName, p.Phone })
            .ToDictionaryAsync(p => p.Id, p => new ProviderLabel(p.DisplayName, p.Phone), cancellationToken);
    }

    private static ConflictedBookingSummary ToSummary(ScannedAssignment row) => new(
        row.BookingId,
        row.AssignmentId,
        row.AssignmentStatus,
        row.BookingStatus,
        row.AssignedByType,
        row.AssignedAt,
        row.CustomerName,
        string.IsNullOrWhiteSpace(row.ServiceName) ? "(no service)" : row.ServiceName,
        row.SlotDate,
        row.StartTime,
        row.EndTime);

    private sealed record ProviderLabel(string DisplayName, string Phone);

    private sealed record ScannedAssignment(
        Guid AssignmentId,
        Guid ProviderId,
        BookingProviderAssignmentStatus AssignmentStatus,
        BookingAssignedByType AssignedByType,
        DateTime AssignedAt,
        Guid BookingId,
        BookingStatus BookingStatus,
        string CustomerName,
        DateOnly SlotDate,
        TimeSpan StartTime,
        TimeSpan EndTime,
        string? ServiceName);
}
