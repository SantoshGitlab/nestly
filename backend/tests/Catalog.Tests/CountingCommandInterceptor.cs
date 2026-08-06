using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Counts the SQL commands a unit of work actually issues, for the N+1
/// regression tests (tasks 253-258).
///
/// An N+1 cannot be detected by asserting on the returned data - a service
/// that loads its rows one-by-one returns exactly the same values as one that
/// batches them. The query count is the behaviour under test, so it is what
/// these tests assert on.
/// </summary>
public sealed class CountingCommandInterceptor : DbCommandInterceptor
{
    private int _count;

    /// <summary>Commands executed since construction (or the last <see cref="Reset"/>).</summary>
    public int CommandCount => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
