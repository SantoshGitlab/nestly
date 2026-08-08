using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("review");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Task 293. Nullable on purpose and permanently - see Review.ProviderId.
        // Restrict, like every other cross-aggregate reference on this entity:
        // deleting a provider must not silently delete the reviews written
        // about them.
        builder.Property(x => x.ProviderId);
        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.ReviewText).HasMaxLength(2000);
        builder.Property(x => x.IssueTags).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsFlagged).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ModeratorNote).HasMaxLength(1000);
        builder.Property(x => x.ModeratedByAdminUserId);
        builder.Property(x => x.ModeratedAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // One primary review per booking (SRS 11.16.3).
        builder.HasIndex(x => x.BookingId).IsUnique();
        builder.HasIndex(x => x.ServiceId);

        // Task 293: the per-provider rating aggregate's exact filter -
        // (provider, visible-only). It runs on the booking detail and the
        // live tracking screen, which are polled, so it must not be a scan.
        builder.HasIndex(x => new { x.ProviderId, x.Status });

        // The admin moderation screen (task 122) filters on status and the
        // flagged marker together (e.g. "hidden and flagged").
        builder.HasIndex(x => new { x.Status, x.IsFlagged });
    }
}
