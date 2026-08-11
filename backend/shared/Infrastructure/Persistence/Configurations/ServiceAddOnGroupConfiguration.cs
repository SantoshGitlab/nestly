using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceAddOnGroupConfiguration : IEntityTypeConfiguration<ServiceAddOnGroup>
{
    public void Configure(EntityTypeBuilder<ServiceAddOnGroup> builder)
    {
        builder.ToTable("service_add_on_group");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasIndex(x => x.ServiceId);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SelectionType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MinSelect).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.MaxSelect);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
    }
}
