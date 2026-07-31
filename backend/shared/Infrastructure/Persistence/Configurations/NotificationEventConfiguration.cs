using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("notification_event");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.BookingId);
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.SupportTicketId);
        builder.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(x => x.SupportTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Channel).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Recipient).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).HasColumnType("text");
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ErrorReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.SentAtUtc);

        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.EventType, x.Status });
    }
}
