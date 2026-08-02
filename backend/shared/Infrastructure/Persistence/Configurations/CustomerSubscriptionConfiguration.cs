using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerSubscriptionConfiguration : IEntityTypeConfiguration<CustomerSubscription>
{
    public void Configure(EntityTypeBuilder<CustomerSubscription> builder)
    {
        builder.ToTable("customer_subscription");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // PlanId is deliberately not a foreign key - see CustomerSubscription's
        // doc comment: traceability only, same convention as
        // Booking.SlotWindowId, since every plan term is already snapshotted
        // onto this row and must keep reading those snapshots even if the
        // source plan is later edited or deleted.
        builder.Property(x => x.PlanId).IsRequired();

        builder.Property(x => x.PlanNameSnapshot).IsRequired().HasMaxLength(150);
        builder.Property(x => x.PriceSnapshot).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.BillingCycleSnapshot).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FreeVisitsIncludedSnapshot).IsRequired();
        builder.Property(x => x.DiscountPercentSnapshot).IsRequired().HasPrecision(5, 2);
        builder.Property(x => x.PrioritySlotFlagSnapshot).IsRequired();

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CurrentPeriodStartUtc).IsRequired();
        builder.Property(x => x.CurrentPeriodEndUtc).IsRequired();
        builder.Property(x => x.FreeVisitsRemaining).IsRequired();
        builder.Property(x => x.NextBillingDateUtc).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();
        builder.Property(x => x.LastPaymentFailureReason).HasMaxLength(500);
        builder.Property(x => x.ExpiringSoonNotifiedForPeriodEndUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CancelledAtUtc);

        // Task 179/181: a customer can have at most one *live* subscription
        // at a time (Active or PaymentFailed) - a partial unique index, not
        // just an app-level check, so two concurrent "subscribe" requests
        // from the same customer cannot both win. A Cancelled/Expired row
        // never counts (see CustomerSubscription's doc comment: "can never
        // be reactivated, only re-subscribed as a new one"), so those are
        // excluded from the filter and a customer can accumulate any number
        // of historical rows.
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("\"status\" IN ('Active', 'PaymentFailed')");

        // Supports the billing job's due-for-charge sweep (task 178).
        builder.HasIndex(x => x.NextBillingDateUtc);
    }
}
