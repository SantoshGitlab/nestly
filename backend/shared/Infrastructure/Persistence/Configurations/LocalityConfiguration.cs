using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class LocalityConfiguration : IEntityTypeConfiguration<Locality>
{
    public void Configure(EntityTypeBuilder<Locality> builder)
    {
        builder.ToTable("locality");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.ZoneId).IsRequired();
        builder.HasOne(x => x.Zone)
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ZoneId, x.Name }).IsUnique();

        builder.Property(x => x.PincodeId).IsRequired();
        builder.HasOne(x => x.Pincode)
            .WithMany()
            .HasForeignKey(x => x.PincodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PincodeId);
    }
}
