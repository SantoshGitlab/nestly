using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SlotBookingCounterConfiguration : IEntityTypeConfiguration<SlotBookingCounter>
{
    public void Configure(EntityTypeBuilder<SlotBookingCounter> builder)
    {
        builder.ToTable("slot_booking_counter");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SlotWindowId).IsRequired();
        builder.Property(x => x.SlotDate).IsRequired();
        builder.Property(x => x.BookedCount).IsRequired();

        // One counter row per window+day - required for correctness, not
        // just lookup speed: SlotCapacityRepository relies on this unique
        // constraint to arbitrate which of two concurrent first-booking
        // requests gets to create the row (task 135c).
        builder.HasIndex(x => new { x.SlotWindowId, x.SlotDate }).IsUnique();

        builder.HasOne<SlotWindow>()
            .WithMany()
            .HasForeignKey(x => x.SlotWindowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
