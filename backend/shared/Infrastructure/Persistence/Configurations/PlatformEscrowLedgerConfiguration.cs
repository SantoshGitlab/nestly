using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PlatformEscrowLedgerConfiguration : IEntityTypeConfiguration<PlatformEscrowLedger>
{
    public void Configure(EntityTypeBuilder<PlatformEscrowLedger> builder)
    {
        builder.ToTable("platform_escrow_ledger");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.EntryType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.BalanceAfter).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.SourceType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SourceReferenceId);
        builder.Property(x => x.ProviderId);
        builder.Property(x => x.CommissionAmount).HasPrecision(12, 2);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Per-booking held-balance derivation filters/orders by this pair.
        builder.HasIndex(x => new { x.BookingId, x.CreatedAtUtc });
        // Platform-wide running balance reads order by this alone.
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
