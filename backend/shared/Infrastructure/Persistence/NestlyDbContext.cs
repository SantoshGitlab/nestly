using Microsoft.EntityFrameworkCore;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence;

public sealed class NestlyDbContext : DbContext
{
    public DbSet<State> States { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Pincode> Pincodes { get; set; }
    public DbSet<Locality> Localities { get; set; }

    public NestlyDbContext(DbContextOptions<NestlyDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestlyDbContext).Assembly);
    }
}
