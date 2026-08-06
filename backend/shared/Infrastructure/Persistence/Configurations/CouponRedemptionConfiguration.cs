using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemption");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CouponId).IsRequired();
        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        // One booking snapshots at most one coupon code.
        builder.HasIndex(x => x.BookingId).IsUnique();

        builder.Property(x => x.DiscountAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RedeemedAtUtc).IsRequired();

        // Non-unique: supports UsageLimitPerCustomer > 1, and these rows are
        // history, not the enforcement point - they are written after the
        // booking they reference exists, so they cannot arbitrate a cap that
        // had to be claimed before it. UsageLimitPerCustomer is enforced by
        // the unique-indexed counter in
        // CouponCustomerRedemptionCounterConfiguration, claimed atomically at
        // reservation time (NESTLY-009). This index serves lookups only.
        builder.HasIndex(x => new { x.CouponId, x.CustomerId });
    }
}
