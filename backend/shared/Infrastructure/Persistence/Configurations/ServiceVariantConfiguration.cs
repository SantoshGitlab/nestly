using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceVariantConfiguration : IEntityTypeConfiguration<ServiceVariant>
{
    public void Configure(EntityTypeBuilder<ServiceVariant> builder)
    {
        builder.ToTable("service_variant");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasIndex(x => x.ServiceId);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.DurationMinutes).IsRequired();
        builder.Property(x => x.InclusionsOverride).HasMaxLength(4000);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
    }
}
