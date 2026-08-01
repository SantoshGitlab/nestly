using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerPayoutConfiguration : IEntityTypeConfiguration<PartnerPayout>
{
    public void Configure(EntityTypeBuilder<PartnerPayout> builder)
    {
        builder.ToTable("partner_payout");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PeriodStart).IsRequired();
        builder.Property(x => x.PeriodEnd).IsRequired();
        builder.Property(x => x.TotalAmount).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PayoutReference).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.PartnerId, x.PeriodStart, x.PeriodEnd });
        builder.HasIndex(x => x.Status);
    }
}
