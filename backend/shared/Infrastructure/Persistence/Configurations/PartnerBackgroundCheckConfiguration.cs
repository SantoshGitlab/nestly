using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerBackgroundCheckConfiguration : IEntityTypeConfiguration<PartnerBackgroundCheck>
{
    public void Configure(EntityTypeBuilder<PartnerBackgroundCheck> builder)
    {
        builder.ToTable("partner_background_check");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CheckedBy).IsRequired();
        builder.Property(x => x.CheckedAt).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        // Append-only (re-checks add rows); the activation gate always reads
        // the most recent row per partner.
        builder.HasIndex(x => new { x.PartnerId, x.CheckedAt });
    }
}
