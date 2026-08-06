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

        // NESTLY-006: makes a duplicate Hold structurally impossible even if
        // EscrowService.HoldAsync is ever called twice for the same payment
        // (e.g. the application-level webhook guard is bypassed or has a
        // bug). SourceReferenceId is the originating PaymentTransaction's id
        // on a Hold entry, and a booking has at most one PaymentTransaction
        // ever (see PaymentTransactionConfiguration's unique BookingId
        // index), so one Hold per transaction is the correct natural key -
        // scoped to Hold rows only, since Release entries legitimately reuse
        // SourceReferenceId for a different purpose (refund/payout tracing).
        builder.HasIndex(x => x.SourceReferenceId)
            .IsUnique()
            .HasFilter("entry_type = 'Hold'");
    }
}
