using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nestly.Infrastructure.Persistence;

namespace Nestly.CustomerManagement.Tests;

/// <summary>
/// A throwaway <see cref="NestlyDbContext"/> backed by an in-memory SQLite
/// database. Same pattern as Identity.Tests/TestDatabase.cs and
/// Catalog.Tests/TestDatabase.cs - kept as its own copy per test project
/// rather than shared, matching that existing convention.
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
            .Options;

        using var context = new NestlyDbContext(Options);
        context.Database.EnsureCreated();
    }

    public DbContextOptions<NestlyDbContext> Options { get; }

    /// <summary>A fresh context over the same database, so no test passes purely because an entity was still tracked by the context that saved it.</summary>
    public NestlyDbContext CreateContext() => new(Options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
