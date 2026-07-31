using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("service");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasIndex(x => x.CategoryId);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ShortDescription).HasMaxLength(500);
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.Inclusions).IsRequired().HasMaxLength(4000).HasDefaultValue(string.Empty);
        builder.Property(x => x.Exclusions).IsRequired().HasMaxLength(4000).HasDefaultValue(string.Empty);
        builder.Property(x => x.CancellationPolicy).HasMaxLength(2000);
        builder.Property(x => x.ReschedulePolicy).HasMaxLength(2000);
        builder.Property(x => x.DurationMinutes).IsRequired().HasDefaultValue(60);
        builder.Property(x => x.IsFeatured).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.SeoTitle).HasMaxLength(200);
        builder.Property(x => x.SeoMetaDescription).HasMaxLength(500);
        builder.Property(x => x.PricingType).IsRequired().HasMaxLength(20).HasConversion<string>().HasDefaultValue(ServicePricingType.Fixed);
        builder.Property(x => x.IsTaxApplicable).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsAddOnAllowed).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsQuantityAllowed).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsInspectionBased).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsSlotRequired).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsAddressRequired).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsCustomerNoteAllowed).IsRequired().HasDefaultValue(true);
    }
}
