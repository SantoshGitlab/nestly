using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CouponCustomerRedemptionCounterConfiguration : IEntityTypeConfiguration<CouponCustomerRedemptionCounter>
{
    public void Configure(EntityTypeBuilder<CouponCustomerRedemptionCounter> builder)
    {
        builder.ToTable("coupon_customer_redemption_counter");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CouponId).IsRequired();
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.ReservedCount).IsRequired();

        // One counter row per coupon+customer - required for correctness, not
        // just lookup speed: CouponRepository relies on this unique constraint
        // to arbitrate which of two concurrent first-redemption requests gets
        // to create the row (NESTLY-009), exactly as SlotBookingCounter's
        // unique index does for per-slot capacity.
        builder.HasIndex(x => new { x.CouponId, x.CustomerId }).IsUnique();

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
