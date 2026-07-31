using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        builder.ToTable("wallet_ledger");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.EntryType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.BalanceAfter).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.SourceType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SourceReferenceId);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Balance-as-of-latest-entry reads filter/order by this pair.
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
    }
}
