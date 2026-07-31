using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Catalog.Tests;

/// <summary>
/// A throwaway <see cref="NestlyDbContext"/> backed by an in-memory SQLite
/// database, created from the real entity configurations via
/// <c>EnsureCreated</c>.
///
/// SQLite rather than the EF in-memory provider because these tests assert on
/// real relational behaviour - unique indexes and foreign key constraints in
/// particular, which the in-memory provider silently ignores. The connection
/// is held open for the fixture's lifetime: an in-memory SQLite database is
/// destroyed the moment its last connection closes.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<NestlyDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            // Same fix DependencyInjection.AddInfrastructure wires up for
            // the real app - see NewOwnedChildEntityInterceptor's doc
            // comment. Tests build their DbContextOptions directly rather
            // than through DI, so it has to be added here too.
            .AddInterceptors(new NewOwnedChildEntityInterceptor())
            .Options;

        using var context = new NestlyDbContext(Options);
        context.Database.EnsureCreated();

        // SQLite doesn't enforce foreign keys unless explicitly told to.
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
    }

    public DbContextOptions<NestlyDbContext> Options { get; }

    /// <summary>
    /// A fresh context over the same database. Each unit of work gets its own
    /// so a test cannot pass purely because an entity was still tracked by the
    /// context that saved it.
    /// </summary>
    public NestlyDbContext CreateContext()
    {
        var context = new NestlyDbContext(Options);
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
