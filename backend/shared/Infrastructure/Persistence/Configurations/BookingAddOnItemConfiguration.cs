using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingAddOnItemConfiguration : IEntityTypeConfiguration<BookingAddOnItem>
{
    public void Configure(EntityTypeBuilder<BookingAddOnItem> builder)
    {
        builder.ToTable("booking_addon_item");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingItemId).IsRequired();

        // ServiceAddOnId is deliberately not a foreign key - see Booking's doc comment.
        builder.Property(x => x.ServiceAddOnId).IsRequired();
        builder.Property(x => x.NameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitPriceSnapshot).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.LineTotalSnapshot).IsRequired().HasPrecision(12, 2);

        // Phase 3 catalog redesign: AddOnGroupId is deliberately not a
        // foreign key, same convention as ServiceAddOnId above. Null when
        // the selected add-on was ungrouped (today's default).
        builder.Property(x => x.AddOnGroupId);
        builder.Property(x => x.GroupNameSnapshot).HasMaxLength(200);

        builder.HasIndex(x => x.BookingItemId);
    }
}
