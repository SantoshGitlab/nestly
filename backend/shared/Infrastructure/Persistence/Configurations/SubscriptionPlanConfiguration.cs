using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plan");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Price).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.BillingCycle).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FreeVisitsIncluded).IsRequired();
        builder.Property(x => x.DiscountPercent).IsRequired().HasPrecision(5, 2);
        builder.Property(x => x.PrioritySlotFlag).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);

        builder.HasIndex(x => x.IsActive);
    }
}
