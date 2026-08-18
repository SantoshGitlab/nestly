using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerAmcContractConfiguration : IEntityTypeConfiguration<CustomerAmcContract>
{
    public void Configure(EntityTypeBuilder<CustomerAmcContract> builder)
    {
        builder.ToTable("customer_amc_contract");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // PlanId is deliberately not a foreign key - traceability only, same
        // convention as CustomerSubscription.PlanId: every plan term is
        // already snapshotted onto this row (see the class doc comment).
        builder.Property(x => x.PlanId).IsRequired();

        builder.Property(x => x.PlanNameSnapshot).IsRequired().HasMaxLength(150);
        builder.Property(x => x.CategoryIdSnapshot).IsRequired();
        builder.Property(x => x.PriceSnapshot).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.TermMonthsSnapshot).IsRequired();
        builder.Property(x => x.VisitsIncludedSnapshot).IsRequired();
        builder.Property(x => x.AssetLabel).IsRequired().HasMaxLength(150);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StartDateUtc).IsRequired();
        builder.Property(x => x.EndDateUtc).IsRequired();
        builder.Property(x => x.VisitsRemaining).IsRequired();

        builder.Property(x => x.PaymentTransactionId);
        builder.HasOne<PaymentTransaction>()
            .WithMany()
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ExpiringSoonNotifiedForEndDateUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CancelledAtUtc);

        builder.HasIndex(x => x.CustomerId);
        // Supports the renewal report's expiring/exhausted horizon query and
        // the scheduled expiry sweep, mirroring CustomerSubscriptionConfiguration's
        // NextBillingDateUtc index for the same "find due rows fast" reason.
        builder.HasIndex(x => x.EndDateUtc);
        builder.HasIndex(x => x.Status);
    }
}
