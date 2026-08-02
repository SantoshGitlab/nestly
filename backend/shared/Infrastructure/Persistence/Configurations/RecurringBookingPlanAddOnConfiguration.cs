using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class RecurringBookingPlanAddOnConfiguration : IEntityTypeConfiguration<RecurringBookingPlanAddOn>
{
    public void Configure(EntityTypeBuilder<RecurringBookingPlanAddOn> builder)
    {
        builder.ToTable("recurring_booking_plan_addon");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecurringBookingPlanId).IsRequired();
        builder.Property(x => x.AddOnId).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();

        builder.HasIndex(x => x.RecurringBookingPlanId);
    }
}
