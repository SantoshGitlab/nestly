using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceCityPriceConfiguration : IEntityTypeConfiguration<ServiceCityPrice>
{
    public void Configure(EntityTypeBuilder<ServiceCityPrice> builder)
    {
        builder.ToTable("service_city_price");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ServiceId, x.CityId }).IsUnique();
    }
}
