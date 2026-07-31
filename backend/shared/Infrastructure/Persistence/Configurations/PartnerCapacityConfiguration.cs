using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerCapacityConfiguration : IEntityTypeConfiguration<PartnerCapacity>
{
    public void Configure(EntityTypeBuilder<PartnerCapacity> builder)
    {
        builder.ToTable("partner_capacity");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PartnerId).IsUnique();

        builder.Property(x => x.MaxJobsPerDay);
        builder.Property(x => x.MaxJobsPerSlot);
    }
}
