using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerRatingConfiguration : IEntityTypeConfiguration<CustomerRating>
{
    public void Configure(EntityTypeBuilder<CustomerRating> builder)
    {
        builder.ToTable("customer_rating");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ProviderId).IsRequired();
        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // One rating per booking (mirrors Review's "one primary review per booking").
        builder.HasIndex(x => x.BookingId).IsUnique();

        // The Customer 360 view's aggregate + recent-ratings read (admin-api).
        builder.HasIndex(x => x.CustomerId);
    }
}
