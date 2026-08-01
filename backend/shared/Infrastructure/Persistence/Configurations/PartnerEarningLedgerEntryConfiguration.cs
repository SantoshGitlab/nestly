using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerEarningLedgerEntryConfiguration : IEntityTypeConfiguration<PartnerEarningLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PartnerEarningLedgerEntry> builder)
    {
        builder.ToTable("partner_earning_ledger");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.EntryType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.BalanceAfter).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.SourceType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SourceReferenceId);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Balance-as-of-latest-entry reads filter/order by this pair, and
        // payout batch calculation sums entries within a period.
        builder.HasIndex(x => new { x.PartnerId, x.CreatedAtUtc });
    }
}
