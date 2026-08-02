using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ReferralMilestoneConfiguration : IEntityTypeConfiguration<ReferralMilestone>
{
    public void Configure(EntityTypeBuilder<ReferralMilestone> builder)
    {
        builder.ToTable("referral_milestone");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ThresholdCount).IsRequired();
        builder.HasIndex(x => x.ThresholdCount).IsUnique();

        builder.Property(x => x.BonusType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BonusValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
