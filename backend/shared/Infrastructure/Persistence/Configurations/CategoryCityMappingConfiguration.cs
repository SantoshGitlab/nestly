using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CategoryCityMappingConfiguration : IEntityTypeConfiguration<CategoryCityMapping>
{
    public void Configure(EntityTypeBuilder<CategoryCityMapping> builder)
    {
        builder.ToTable("category_city_mapping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CategoryId, x.CityId }).IsUnique();
        builder.HasIndex(x => x.CityId);
    }
}
