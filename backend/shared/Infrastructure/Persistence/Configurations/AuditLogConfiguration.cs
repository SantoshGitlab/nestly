using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActorId);

        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);

        // jsonb rather than text: the admin audit screen (SRS 31.6) needs to
        // filter on individual changed fields, which a text column cannot index.
        builder.Property(x => x.OldValues).HasColumnType("jsonb");
        builder.Property(x => x.NewValues).HasColumnType("jsonb");

        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.OccurredOnUtc).IsRequired();

        // The audit screen is browsed newest-first and filtered by subject or
        // by actor; these three cover those access paths.
        builder.HasIndex(x => x.OccurredOnUtc).IsDescending();
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => new { x.ActorType, x.ActorId });
    }
}
