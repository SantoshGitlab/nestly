using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_user");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.LockedUntilUtc);

        // Nullable: an account authenticates before a Super Admin ever
        // assigns it a role (task 96c). SetNull on delete rather than
        // Restrict — unlike role_permission_mapping's FKs, deleting a role
        // should not be blocked by accounts that merely reference it; it
        // should just leave them unassigned.
        builder.Property(x => x.RoleId);
        builder.HasOne<AdminRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.RoleId);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
