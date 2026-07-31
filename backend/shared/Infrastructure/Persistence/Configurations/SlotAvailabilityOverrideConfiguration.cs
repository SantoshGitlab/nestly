using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SlotAvailabilityOverrideConfiguration : IEntityTypeConfiguration<SlotAvailabilityOverride>
{
    public void Configure(EntityTypeBuilder<SlotAvailabilityOverride> builder)
    {
        builder.ToTable("slot_availability_override");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);

        builder.Property(x => x.CityId).IsRequired();
        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional narrowing scope (SRS 12.10.2): a null value here means the
        // override is not narrowed to that dimension. Set null on delete of
        // the referenced row rather than cascading - the override still
        // makes sense at whatever scope remains (e.g. still blocks the whole
        // city/date if the slot window it named is later removed).
        builder.HasOne(x => x.SlotWindow)
            .WithMany()
            .HasForeignKey(x => x.SlotWindowId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.CityId, x.Date });
    }
}
