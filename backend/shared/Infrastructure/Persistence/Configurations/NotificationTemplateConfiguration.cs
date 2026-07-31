using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_template");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Channel).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Subject).HasMaxLength(300);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);

        // The renderer looks up exactly one row per (EventType, Channel) -
        // same invariant the old fixed dictionary enforced by construction
        // (one entry per tuple key).
        builder.HasIndex(x => new { x.EventType, x.Channel }).IsUnique();
        builder.HasIndex(x => x.TemplateKey).IsUnique();
    }
}
