using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SlotWindowConfiguration : IEntityTypeConfiguration<SlotWindow>
{
    public void Configure(EntityTypeBuilder<SlotWindow> builder)
    {
        builder.ToTable("slot_window");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.MaxBookingsPerSlot);

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId);
    }
}
