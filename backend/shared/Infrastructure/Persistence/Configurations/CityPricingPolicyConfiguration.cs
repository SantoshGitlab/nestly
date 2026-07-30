using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CityPricingPolicyConfiguration : IEntityTypeConfiguration<CityPricingPolicy>
{
    public void Configure(EntityTypeBuilder<CityPricingPolicy> builder)
    {
        builder.ToTable("city_pricing_policy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VisitCharge).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.TaxPercentage).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.PlatformFee).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId).IsUnique();
    }
}
