using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceAddOnConfiguration : IEntityTypeConfiguration<ServiceAddOn>
{
    public void Configure(EntityTypeBuilder<ServiceAddOn> builder)
    {
        builder.ToTable("service_addon");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasIndex(x => x.ServiceId);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
    }
}
