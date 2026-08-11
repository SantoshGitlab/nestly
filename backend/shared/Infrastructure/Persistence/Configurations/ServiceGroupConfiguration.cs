using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceGroupConfiguration : IEntityTypeConfiguration<ServiceGroup>
{
    public void Configure(EntityTypeBuilder<ServiceGroup> builder)
    {
        builder.ToTable("service_group");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasIndex(x => x.CategoryId);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
    }
}
