using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class RecurringBookingOccurrenceConfiguration : IEntityTypeConfiguration<RecurringBookingOccurrence>
{
    public void Configure(EntityTypeBuilder<RecurringBookingOccurrence> builder)
    {
        builder.ToTable("recurring_booking_occurrence");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecurringBookingPlanId).IsRequired();
        builder.HasOne<RecurringBookingPlan>()
            .WithMany()
            .HasForeignKey(x => x.RecurringBookingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ScheduledDate).IsRequired();
        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BookingId);
        builder.Property(x => x.SkipReason).HasMaxLength(500);
        builder.Property(x => x.ProcessedAtUtc).IsRequired();

        // The scheduler's idempotency guard (class doc comment) - one row per
        // (plan, scheduled date), regardless of outcome.
        builder.HasIndex(x => new { x.RecurringBookingPlanId, x.ScheduledDate }).IsUnique();
    }
}
