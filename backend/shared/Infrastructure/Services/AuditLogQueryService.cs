using Microsoft.EntityFrameworkCore;
using Nestly.Application.Auditing;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Filterable audit trail search (task 130) over the same <c>audit_log</c>
/// table <see cref="Nestly.Infrastructure.Auditing.AuditLogWriter"/> writes
/// to. Read-only by design — an audit trail is append-only, so this type has
/// no business mutating anything (<see cref="AuditLog"/> itself exposes no
/// mutators).
/// </summary>
public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private static readonly Error InvalidDateRange = Error.Validation(
        "AuditLog.InvalidDateRange", "The date range start must not be after its end.");

    private readonly NestlyDbContext _context;

    public AuditLogQueryService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedAuditLogResponse>> SearchAsync(
        AuditLogFilterRequest filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc > filter.ToUtc)
        {
            return Result.Failure<PagedAuditLogResponse>(InvalidDateRange);
        }

        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = filter.PageSize switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => filter.PageSize
        };

        IQueryable<AuditLog> query = _context.Set<AuditLog>().AsNoTracking();
        query = ApplyFilters(query, filter);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Newest first: matches the audit screen's natural browsing order and
        // the descending index AuditLogConfiguration defines for exactly this
        // access path.
        List<AuditLog> pageEntries = await query
            .OrderByDescending(entry => entry.OccurredOnUtc)
            .ApplyPaging(page, pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = pageEntries.Select(ToResponse).ToList();
        return Result.Success(new PagedAuditLogResponse(items, totalCount, page, pageSize));
    }

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> query, AuditLogFilterRequest filter)
    {
        if (filter.ActorType.HasValue)
        {
            query = query.Where(entry => entry.ActorType == filter.ActorType.Value);
        }

        if (filter.ActorId.HasValue)
        {
            query = query.Where(entry => entry.ActorId == filter.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(entry => entry.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(entry => entry.Action.Contains(filter.Action));
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredOnUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredOnUtc <= filter.ToUtc.Value);
        }

        if (filter.Outcome.HasValue)
        {
            query = ApplyOutcomeFilter(query, filter.Outcome.Value);
        }

        return query;
    }

    /// <summary>
    /// Translates an <see cref="AuditOutcome"/> into the same
    /// Deny-then-Grant-then-Failure-then-Success precedence
    /// <see cref="AuditOutcomeClassifier.Classify"/> uses, expressed as
    /// SQL-translatable substring checks (plain <c>string.Contains</c> rather
    /// than the <see cref="StringComparison"/> overload, which EF Core cannot
    /// translate) rather than calling the classifier itself, which EF Core
    /// cannot translate to SQL either.
    /// </summary>
    private static IQueryable<AuditLog> ApplyOutcomeFilter(IQueryable<AuditLog> query, AuditOutcome outcome) =>
        outcome switch
        {
            AuditOutcome.Deny => query.Where(entry =>
                entry.Action.Contains(AuditOutcomeClassifier.DeniedMarker)),
            AuditOutcome.Grant => query.Where(entry =>
                !entry.Action.Contains(AuditOutcomeClassifier.DeniedMarker) &&
                entry.Action.Contains(AuditOutcomeClassifier.GrantedMarker)),
            AuditOutcome.Failure => query.Where(entry =>
                !entry.Action.Contains(AuditOutcomeClassifier.DeniedMarker) &&
                !entry.Action.Contains(AuditOutcomeClassifier.GrantedMarker) &&
                entry.Action.Contains(AuditOutcomeClassifier.FailedMarker)),
            _ => query.Where(entry =>
                !entry.Action.Contains(AuditOutcomeClassifier.DeniedMarker) &&
                !entry.Action.Contains(AuditOutcomeClassifier.GrantedMarker) &&
                !entry.Action.Contains(AuditOutcomeClassifier.FailedMarker))
        };

    private static AuditLogEntryResponse ToResponse(AuditLog entry) => new(
        entry.Id,
        entry.ActorType,
        entry.ActorId,
        entry.EntityName,
        entry.EntityId,
        entry.Action,
        AuditOutcomeClassifier.Classify(entry.Action),
        entry.OldValues,
        entry.NewValues,
        entry.IpAddress,
        entry.CorrelationId,
        entry.OccurredOnUtc);
}
