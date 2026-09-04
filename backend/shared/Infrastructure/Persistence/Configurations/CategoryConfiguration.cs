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
        builder.Property(x => x.PageBannerUrl).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsFeatured).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.SeoTitle).HasMaxLength(200);
        builder.Property(x => x.SeoMetaDescription).HasMaxLength(500);

        // Phase 3 catalog redesign: the first real FK constraint among the
        // catalog entities (Category/Service/ServiceAddOn otherwise cross-
        // reference each other via unconstrained indexed Guid columns only) -
        // parent/child is genuine same-aggregate-type tree integrity, so
        // Restrict prevents orphaning a subcategory by deleting its parent.
        builder.Property(x => x.ParentCategoryId);
        builder.HasIndex(x => x.ParentCategoryId);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mirrors ServiceConfiguration's ServiceGroupId FK: SetNull is a
        // safety net only - CategoryGroupManagementService is the primary
        // guard, refusing to delete a group still assigned to a category.
        builder.Property(x => x.CategoryGroupId);
        builder.HasIndex(x => x.CategoryGroupId);
        builder.HasOne<CategoryGroup>()
            .WithMany()
            .HasForeignKey(x => x.CategoryGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
