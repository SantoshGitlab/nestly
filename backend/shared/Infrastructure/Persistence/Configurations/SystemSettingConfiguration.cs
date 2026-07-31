using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_setting");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GroupKey).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.GroupKey).IsUnique();

        // jsonb, not text: the admin Settings UI reads/writes whole-group
        // values only today, but jsonb keeps ad-hoc inspection/querying of
        // individual fields possible later without a migration, same
        // reasoning as AuditLog.OldValues/NewValues.
        builder.Property(x => x.ValueJson).IsRequired().HasColumnType("jsonb");

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);
    }
}
