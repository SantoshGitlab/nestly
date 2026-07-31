using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupon");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.DiscountType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DiscountValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(12, 2);
        builder.Property(x => x.MinOrderAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.ValidFromUtc).IsRequired();
        builder.Property(x => x.ValidToUtc).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UsageLimitTotal);
        builder.Property(x => x.UsageLimitPerCustomer);
        builder.Property(x => x.RedemptionCount).IsRequired();
        builder.Property(x => x.ApplicableCategoryId);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.ApplicableCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CustomerSegment).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.ValidFromUtc, x.ValidToUtc });
    }
}
