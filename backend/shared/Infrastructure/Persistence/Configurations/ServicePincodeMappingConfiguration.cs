using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServicePincodeMappingConfiguration : IEntityTypeConfiguration<ServicePincodeMapping>
{
    public void Configure(EntityTypeBuilder<ServicePincodeMapping> builder)
    {
        builder.ToTable("service_pincode_mapping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PincodeId).IsRequired();
        builder.HasOne<Pincode>()
            .WithMany()
            .HasForeignKey(x => x.PincodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ServiceId, x.PincodeId }).IsUnique();
        builder.HasIndex(x => x.PincodeId);
    }
}
