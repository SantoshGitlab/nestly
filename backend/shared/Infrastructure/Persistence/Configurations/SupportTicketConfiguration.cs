using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_ticket");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional - mandatory-for-booking-issues is a service-layer rule
        // (SRS 11.18.2), not a database constraint.
        builder.Property(x => x.BookingId);
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Category).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Priority).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ResolutionSummary).HasMaxLength(2000);
        builder.Property(x => x.IsDisputed).IsRequired();
        builder.Property(x => x.DisputeOutcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DisputeResolvedAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasMany(x => x.Comments)
            .WithOne()
            .HasForeignKey(x => x.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => x.Status);
    }
}
