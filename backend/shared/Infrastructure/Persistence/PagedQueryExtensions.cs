namespace Nestly.Infrastructure.Persistence;

/// <summary>
/// One place where "page N of size M" becomes a SQL OFFSET/LIMIT (task 261).
///
/// Every paged repository used to write <c>.Skip((page - 1) * pageSize)</c>
/// inline. That expression is evaluated in unchecked <see cref="int"/>
/// arithmetic, so it silently wraps: the admin paging validators bound
/// <c>PageSize</c> to 1..100 but bound <c>Page</c> only with
/// <c>GreaterThanOrEqualTo(1)</c>, which means <c>Page=2000000000</c> passes
/// validation, multiplies out to -1863463012, and reaches PostgreSQL as a
/// negative OFFSET. Postgres rejects that outright ("OFFSET must not be
/// negative"), turning a merely absurd page number into an unhandled 500 on
/// every paged admin screen.
///
/// Computing the offset in <see cref="long"/> and saturating at
/// <see cref="int.MaxValue"/> keeps the arithmetic honest: a page past the end
/// of the table returns an empty page, which is what a caller asking for page
/// two billion should get.
/// </summary>
public static class PagedQueryExtensions
{
    /// <summary>Same ceiling the admin *Validators.cs and AuditLogQueryService already enforce.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page size substituted when a caller supplies a non-positive one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Coerces caller-supplied paging into the supported range. Separate from
    /// <see cref="ApplyPaging{T}"/> so a service can echo the values actually
    /// used back to the caller instead of the ones it asked for.
    /// </summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int defaultPageSize = DefaultPageSize, int maxPageSize = MaxPageSize) =>
        (
            page < 1 ? 1 : page,
            pageSize switch
            {
                <= 0 => defaultPageSize,
                _ when pageSize > maxPageSize => maxPageSize,
                _ => pageSize
            }
        );

    /// <summary>
    /// The row offset for a 1-based page, never negative and never wrapped.
    /// Saturates at <see cref="int.MaxValue"/> because that is already far
    /// past any real table and <c>Skip</c> takes an <see cref="int"/>.
    /// </summary>
    public static int Offset(int page, int pageSize)
    {
        long offset = (long)(page < 1 ? 0 : page - 1) * (pageSize < 0 ? 0 : pageSize);
        return offset > int.MaxValue ? int.MaxValue : (int)offset;
    }

    /// <summary>
    /// Applies OFFSET/LIMIT for a 1-based page. Call after the ordering
    /// clause, exactly where the hand-written Skip/Take used to sit - an
    /// unordered paged query has no stable page boundaries.
    /// </summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, int page, int pageSize, int defaultPageSize = DefaultPageSize, int maxPageSize = MaxPageSize)
    {
        (int safePage, int safePageSize) = Normalize(page, pageSize, defaultPageSize, maxPageSize);
        return query.Skip(Offset(safePage, safePageSize)).Take(safePageSize);
    }
}
