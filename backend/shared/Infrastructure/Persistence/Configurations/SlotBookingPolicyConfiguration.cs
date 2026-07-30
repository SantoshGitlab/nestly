using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SlotBookingPolicyConfiguration : IEntityTypeConfiguration<SlotBookingPolicy>
{
    public void Configure(EntityTypeBuilder<SlotBookingPolicy> builder)
    {
        builder.ToTable("slot_booking_policy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CutoffMinutes).IsRequired();
        builder.Property(x => x.MaxAdvanceDays).IsRequired();

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId).IsUnique();
    }
}
