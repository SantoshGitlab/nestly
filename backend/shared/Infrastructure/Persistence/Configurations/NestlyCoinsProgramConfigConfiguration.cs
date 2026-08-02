using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class NestlyCoinsProgramConfigConfiguration : IEntityTypeConfiguration<NestlyCoinsProgramConfig>
{
    public void Configure(EntityTypeBuilder<NestlyCoinsProgramConfig> builder)
    {
        builder.ToTable("nestly_coins_program_config");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Audience).IsRequired().HasConversion<string>().HasMaxLength(20);
        // Explicit column name: the snake-case naming convention collapses
        // "Per100" to "per100" (no boundary before a digit run), which would
        // drift from docs/NESTLY-COINS.md's literal "earn_rate_per_100".
        builder.Property(x => x.EarnRatePer100).IsRequired().HasPrecision(12, 2).HasColumnName("earn_rate_per_100");
        builder.Property(x => x.MinimumOrderAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RequireReorder).IsRequired();
        builder.Property(x => x.MaxCoinsPerMonth).HasPrecision(12, 2);
        builder.Property(x => x.ExpiryDays).IsRequired();
        builder.Property(x => x.ClawbackWindowDays).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);

        // One row per audience, enforced at the schema level, not just by
        // convention - GUIDELINES #5 ("one coins program per side").
        builder.HasIndex(x => x.Audience).IsUnique();
    }
}
