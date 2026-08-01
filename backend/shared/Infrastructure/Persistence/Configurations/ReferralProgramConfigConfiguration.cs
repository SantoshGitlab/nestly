using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ReferralProgramConfigConfiguration : IEntityTypeConfiguration<ReferralProgramConfig>
{
    public void Configure(EntityTypeBuilder<ReferralProgramConfig> builder)
    {
        builder.ToTable("referral_program_config");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerRewardType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReferrerRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.RefereeRewardType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RefereeRewardValue).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.MinQualifyingOrderAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.ReferralExpiryDays).IsRequired();
        builder.Property(x => x.MaxReferralsPerCustomer);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);
    }
}
