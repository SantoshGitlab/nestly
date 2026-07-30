using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>
{
    public void Configure(EntityTypeBuilder<BookingItem> builder)
    {
        builder.ToTable("booking_item");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();

        // ServiceId is deliberately not a foreign key - see Booking's doc comment.
        builder.Property(x => x.ServiceId).IsRequired();
        builder.Property(x => x.NameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SlugSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitPriceSnapshot).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.LineTotalSnapshot).IsRequired().HasPrecision(12, 2);

        builder.HasMany(x => x.AddOns)
            .WithOne()
            .HasForeignKey(x => x.BookingItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BookingId);
    }
}
