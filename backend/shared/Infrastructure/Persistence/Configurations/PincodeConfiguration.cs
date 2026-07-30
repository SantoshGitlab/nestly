using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PincodeConfiguration : IEntityTypeConfiguration<Pincode>
{
    public void Configure(EntityTypeBuilder<Pincode> builder)
    {
        builder.ToTable("pincode");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId);
    }
}
