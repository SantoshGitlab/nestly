using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerServiceAreaConfiguration : IEntityTypeConfiguration<PartnerServiceArea>
{
    public void Configure(EntityTypeBuilder<PartnerServiceArea> builder)
    {
        builder.ToTable("partner_service_area");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ZoneId);
        builder.HasOne<Zone>()
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PincodeId);
        builder.HasOne<Pincode>()
            .WithMany()
            .HasForeignKey(x => x.PincodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PartnerId, x.CityId, x.ZoneId, x.PincodeId }).IsUnique();
        builder.HasIndex(x => x.CityId);
    }
}
