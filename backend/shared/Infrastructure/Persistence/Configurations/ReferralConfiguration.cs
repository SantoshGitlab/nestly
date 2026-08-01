using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("referral");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerCustomerId).IsRequired();
        builder.Property(x => x.RefereeCustomerId).IsRequired();

        // One referral per referee, ever (REFERRAL.md "FRAUD / ABUSE
        // PREVENTION" - "cannot be referred twice for repeat rewards").
        builder.HasIndex(x => x.RefereeCustomerId).IsUnique();
        builder.HasIndex(x => x.ReferrerCustomerId);

        builder.Property(x => x.ReferralCodeUsed).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.QualifyingBookingId);

        builder.Property(x => x.ReferrerRewardType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReferrerRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefereeRewardType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RefereeRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.MinQualifyingOrderAmount).IsRequired().HasPrecision(12, 2);

        builder.Property(x => x.ReferrerWalletEntryId);
        builder.Property(x => x.ReferrerCouponId);
        builder.Property(x => x.RefereeWalletEntryId);
        builder.Property(x => x.RefereeCouponId);

        builder.Property(x => x.RegisteredAtUtc).IsRequired();
        builder.Property(x => x.QualifiedAtUtc);
        builder.Property(x => x.RewardedAtUtc);
        builder.Property(x => x.ExpiresAtUtc).IsRequired();

        // Task 175's expiry sweep scans Registered rows past ExpiresAtUtc.
        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        builder.Property(x => x.IsFraudFlagged).IsRequired();
        builder.Property(x => x.FraudReviewNote).HasMaxLength(1000);
        builder.Property(x => x.FraudReviewedByAdminUserId);
        builder.Property(x => x.FraudReviewedAtUtc);
        builder.HasIndex(x => x.IsFraudFlagged);
    }
}
