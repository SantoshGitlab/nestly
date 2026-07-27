using Microsoft.EntityFrameworkCore;

namespace Nestly.Infrastructure.Persistence;

/// <summary>
/// Application database context. Entity configurations are discovered from
/// this assembly (IEntityTypeConfiguration implementations per module).
/// </summary>
public sealed class NestlyDbContext : DbContext
{
    public NestlyDbContext(DbContextOptions<NestlyDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestlyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
