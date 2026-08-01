using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Performance.Tests;

/// <summary>
/// A throwaway <see cref="NestlyDbContext"/> backed by a <em>file-based</em>
/// SQLite database, one per fixture instance, removed on dispose.
///
/// Deliberately not Catalog.Tests/TestDatabase.cs's pattern: that fixture
/// holds a single shared in-memory connection open for its whole lifetime,
/// which is the right choice for correctness tests but cannot be used for
/// real concurrency - Microsoft.Data.Sqlite does not support issuing
/// commands concurrently on one connection from multiple threads (see that
/// file's doc comment, and BookingConcurrencyTests' explanation of why its
/// race tests use forced sequential interleavings instead of Task.WhenAll).
///
/// These are load/perf tests (tasks 135a-c) whose entire point is many
/// simulated customers hitting the database at the same time via
/// Task.WhenAll, each on its own context/connection - exactly what would
/// happen with independent request-scoped DbContexts in production. A
/// temp-file database lets every <see cref="CreateContext"/> open a genuinely
/// separate connection to the same file; SQLite's own locking (WAL mode +
/// busy_timeout below) then arbitrates concurrent writers similarly to how
/// Postgres row/statement locking would, which is what actually exercises
/// SlotCapacityRepository's atomic reservation under real contention rather
/// than a single-connection artifact.
/// </summary>
public sealed class PerfTestDatabase : IDisposable
{
    private const string BusyTimeoutMs = "30000";

    private readonly string _dbPath;

    public PerfTestDatabase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"nestly-perf-{Guid.NewGuid():N}.db");

        Options = new DbContextOptionsBuilder<NestlyDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            // Same fix DependencyInjection.AddInfrastructure wires up for the
            // real app - see NewOwnedChildEntityInterceptor's doc comment.
            .AddInterceptors(new NewOwnedChildEntityInterceptor())
            .Options;

        using var context = new NestlyDbContext(Options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
        context.Database.ExecuteSqlRaw($"PRAGMA busy_timeout = {BusyTimeoutMs};");
    }

    public DbContextOptions<NestlyDbContext> Options { get; }

    /// <summary>
    /// A fresh context, on its own connection, over the same database file.
    /// Pragmas are per-connection, so busy_timeout/foreign_keys are
    /// re-applied every time - a concurrent caller waits (rather than
    /// immediately failing with SQLITE_BUSY) when another connection briefly
    /// holds the write lock, which is what lets many simulated concurrent
    /// bookings resolve correctly instead of erroring on lock contention that
    /// has nothing to do with the business behaviour under test.
    /// </summary>
    public NestlyDbContext CreateContext()
    {
        var context = new NestlyDbContext(Options);
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        context.Database.ExecuteSqlRaw($"PRAGMA busy_timeout = {BusyTimeoutMs};");
        return context;
    }

    public void Dispose()
    {
        // Releases every pooled connection to the file before deleting it -
        // otherwise the delete can silently no-op on platforms that keep a
        // file handle open (or race a still-open WAL/SHM sidecar).
        SqliteConnection.ClearAllPools();

        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup - a lingering temp file is not worth failing the suite over.
            }
        }
    }
}
