using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("booking_status_history");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ToStatus).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.ChangedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.BookingId, x.ChangedAtUtc });
    }
}
