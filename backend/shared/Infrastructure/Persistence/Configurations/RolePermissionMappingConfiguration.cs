using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class RolePermissionMappingConfiguration : IEntityTypeConfiguration<RolePermissionMapping>
{
    public void Configure(EntityTypeBuilder<RolePermissionMapping> builder)
    {
        builder.ToTable("role_permission_mapping");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleId).IsRequired();
        builder.HasOne<AdminRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PermissionId).IsRequired();
        builder.HasOne<AdminPermission>()
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        builder.HasIndex(x => x.PermissionId);
    }
}
