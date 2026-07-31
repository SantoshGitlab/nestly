using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingCancellationConfiguration : IEntityTypeConfiguration<BookingCancellation>
{
    public void Configure(EntityTypeBuilder<BookingCancellation> builder)
    {
        builder.ToTable("booking_cancellation");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Actor).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        builder.Property(x => x.WithinFreeCancellationWindow).IsRequired();
        builder.Property(x => x.CancellationFeeAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefundAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RefundTransactionId);
        builder.Property(x => x.InternalNotes).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // One cancellation record per booking - a cancelled booking can
        // never be cancelled again (BookingLifecycle blocks the transition).
        builder.HasIndex(x => x.BookingId).IsUnique();
    }
}
