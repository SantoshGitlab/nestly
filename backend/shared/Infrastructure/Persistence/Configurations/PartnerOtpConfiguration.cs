using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerOtpConfiguration : IEntityTypeConfiguration<PartnerOtp>
{
    public void Configure(EntityTypeBuilder<PartnerOtp> builder)
    {
        builder.ToTable("partner_otp");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartnerId);
        builder.HasIndex(x => x.PartnerId);
        builder.Property(x => x.Target).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Target);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
