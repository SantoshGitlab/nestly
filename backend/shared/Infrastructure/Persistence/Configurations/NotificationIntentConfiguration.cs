using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class NotificationIntentConfiguration : IEntityTypeConfiguration<NotificationIntent>
{
    public void Configure(EntityTypeBuilder<NotificationIntent> builder)
    {
        builder.ToTable("notification_intent");
        builder.HasKey(x => x.Id);

        // The idempotency rule, enforced by the database rather than by
        // agreement between callers: two writers that both believe they should
        // create the intent for one (event, message) pair cannot both succeed.
        builder.Property(x => x.DedupeKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.DedupeKey).IsUnique();

        builder.Property(x => x.DomainEventId).IsRequired();
        builder.Property(x => x.DomainEventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");

        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastAttemptAtUtc);
        builder.Property(x => x.CompletedAtUtc);
        builder.Property(x => x.LeaseOwner).HasMaxLength(100);
        builder.Property(x => x.LeaseExpiresAtUtc);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.Resolution).HasMaxLength(500);

        // The sweep's candidate query: pending rows, oldest first. Leading with
        // Status keeps the index small in steady state - almost everything in
        // this table is terminal within seconds of being written.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });

        // Correlates the rows one event produced, for the "why did this
        // customer get two messages" question a support agent will eventually
        // ask.
        builder.HasIndex(x => x.DomainEventId);

        // No foreign keys, deliberately. An intent references its subject only
        // through the serialized event payload: the row exists to survive the
        // failure of everything around it, and a cascade or a restrict from a
        // booking or a customer would give the notification path a way to be
        // blocked by, or deleted with, the very data it is trying to talk
        // about.
    }
}
