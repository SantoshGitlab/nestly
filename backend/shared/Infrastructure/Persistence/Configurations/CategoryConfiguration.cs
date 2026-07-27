using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("category");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.IconUrl).HasMaxLength(500);
        builder.Property(x => x.BannerUrl).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsFeatured).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.SeoTitle).HasMaxLength(200);
        builder.Property(x => x.SeoMetaDescription).HasMaxLength(500);
    }
}
