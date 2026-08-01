using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerAvailabilityWindowConfiguration : IEntityTypeConfiguration<PartnerAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<PartnerAvailabilityWindow> builder)
    {
        builder.ToTable("partner_availability_window");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.PartnerId, x.DayOfWeek });
    }
}
