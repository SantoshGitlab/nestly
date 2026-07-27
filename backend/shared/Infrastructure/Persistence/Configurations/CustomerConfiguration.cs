using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mobile).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.Mobile).IsUnique();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(200);
        builder.Property(x => x.State).HasMaxLength(200);
        builder.Property(x => x.Pincode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}
