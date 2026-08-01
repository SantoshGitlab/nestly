using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ReferralMilestoneAwardConfiguration : IEntityTypeConfiguration<ReferralMilestoneAward>
{
    public void Configure(EntityTypeBuilder<ReferralMilestoneAward> builder)
    {
        builder.ToTable("referral_milestone_award");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferralMilestoneId).IsRequired();
        builder.Property(x => x.ReferrerCustomerId).IsRequired();

        // Task 174's idempotency guard: one award per (milestone, referrer), ever.
        builder.HasIndex(x => new { x.ReferralMilestoneId, x.ReferrerCustomerId }).IsUnique();

        builder.Property(x => x.WalletEntryId);
        builder.Property(x => x.CouponId);
        builder.Property(x => x.AwardedAtUtc).IsRequired();
    }
}
