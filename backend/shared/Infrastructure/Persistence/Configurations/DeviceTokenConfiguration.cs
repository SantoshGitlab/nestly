using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_token");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Platform).IsRequired().HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.RegisteredAtUtc).IsRequired();
        builder.Property(x => x.RevokedAtUtc);

        // A device/app-install is unique platform-wide (see DeviceToken's
        // doc comment on ReRegister) - not scoped per customer.
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.IsActive });
    }
}
