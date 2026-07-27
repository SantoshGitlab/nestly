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
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
    }
}
