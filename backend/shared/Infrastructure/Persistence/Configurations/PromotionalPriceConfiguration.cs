using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PromotionalPriceConfiguration : IEntityTypeConfiguration<PromotionalPrice>
{
    public void Configure(EntityTypeBuilder<PromotionalPrice> builder)
    {
        builder.ToTable("promotional_price");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountedPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CityId);
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ServiceId, x.CityId });
    }
}
