using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ProviderReferralProgramConfigConfiguration : IEntityTypeConfiguration<ProviderReferralProgramConfig>
{
    public void Configure(EntityTypeBuilder<ProviderReferralProgramConfig> builder)
    {
        builder.ToTable("provider_referral_program_config");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefereeRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.QualifyingCompletedJobsCount).IsRequired();
        builder.Property(x => x.ReferralExpiryDays).IsRequired();
        builder.Property(x => x.MaxReferralsPerProvider);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);
    }
}
