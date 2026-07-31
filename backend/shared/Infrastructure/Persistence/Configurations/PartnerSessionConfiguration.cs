using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerSessionConfiguration : IEntityTypeConfiguration<PartnerSession>
{
    public void Configure(EntityTypeBuilder<PartnerSession> builder)
    {
        builder.ToTable("partner_session");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasIndex(x => x.PartnerId);
        builder.Property(x => x.RefreshTokenHash).IsRequired().HasMaxLength(500);
        builder.HasIndex(x => x.RefreshTokenHash).IsUnique();
        builder.Property(x => x.IssuedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.DeviceInfo).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
    }
}
