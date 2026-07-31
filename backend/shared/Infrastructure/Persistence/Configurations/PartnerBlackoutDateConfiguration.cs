using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerBlackoutDateConfiguration : IEntityTypeConfiguration<PartnerBlackoutDate>
{
    public void Configure(EntityTypeBuilder<PartnerBlackoutDate> builder)
    {
        builder.ToTable("partner_blackout_date");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasIndex(x => new { x.PartnerId, x.StartDate, x.EndDate });
    }
}
