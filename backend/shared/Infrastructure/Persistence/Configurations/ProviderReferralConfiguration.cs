using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ProviderReferralConfiguration : IEntityTypeConfiguration<ProviderReferral>
{
    public void Configure(EntityTypeBuilder<ProviderReferral> builder)
    {
        builder.ToTable("provider_referral");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerProviderId).IsRequired();
        builder.Property(x => x.RefereeProviderId).IsRequired();

        // One referral per referee, ever (mirrors ReferralConfiguration).
        builder.HasIndex(x => x.RefereeProviderId).IsUnique();
        builder.HasIndex(x => x.ReferrerProviderId);

        builder.Property(x => x.ReferralCodeUsed).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.QualifyingBookingId);

        builder.Property(x => x.ReferrerRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefereeRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.QualifyingCompletedJobsCount).IsRequired();

        builder.Property(x => x.ReferrerEarningEntryId);
        builder.Property(x => x.RefereeEarningEntryId);

        builder.Property(x => x.RegisteredAtUtc).IsRequired();
        builder.Property(x => x.QualifiedAtUtc);
        builder.Property(x => x.RewardedAtUtc);
        builder.Property(x => x.ExpiresAtUtc).IsRequired();

        // Expiry sweep scans Registered rows past ExpiresAtUtc.
        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        builder.Property(x => x.IsFraudFlagged).IsRequired();
        builder.Property(x => x.FraudReviewNote).HasMaxLength(1000);
        builder.Property(x => x.FraudReviewedByAdminUserId);
        builder.Property(x => x.FraudReviewedAtUtc);
        builder.HasIndex(x => x.IsFraudFlagged);
    }
}
