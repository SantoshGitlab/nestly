using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class AdminPermissionConfiguration : IEntityTypeConfiguration<AdminPermission>
{
    public void Configure(EntityTypeBuilder<AdminPermission> builder)
    {
        builder.ToTable("admin_permission");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Module).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Module);

        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
