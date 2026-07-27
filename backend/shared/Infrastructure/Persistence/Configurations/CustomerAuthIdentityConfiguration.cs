using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerAuthIdentityConfiguration : IEntityTypeConfiguration<CustomerAuthIdentity>
{
    public void Configure(EntityTypeBuilder<CustomerAuthIdentity> builder)
    {
        builder.ToTable("customer_auth_identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasIndex(x => x.CustomerId);
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Identifier).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.Provider, x.Identifier }).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.IsPrimary).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
