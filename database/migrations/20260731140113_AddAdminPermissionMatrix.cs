using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 96a: the AdminUser -&gt; AdminRole assignment column
    /// (<see cref="AdminUser.RoleId"/>, needed so token issuance can resolve
    /// an account's permission claims — task 96c) plus the permission-matrix
    /// seed data itself: every row of <see cref="AdminPermissionCatalog"/>
    /// and <see cref="AdminRoleNames"/>, and the grants between them (SRS
    /// 12.2.2, 12.2.3). Ids are deterministic (<see cref="DeterministicId"/>)
    /// rather than <c>Guid.NewGuid()</c> so re-running this migration against
    /// a fresh database always produces the exact same rows — required for
    /// <see cref="Down"/> to be able to identify and remove exactly what
    /// <see cref="Up"/> inserted.
    /// </summary>
    public partial class AddAdminPermissionMatrix : Migration
    {
        // Fixed rather than DateTime.UtcNow: a migration's Up() runs once
        // per environment at whatever time it's applied, but the rows it
        // inserts are reference data, not an event with a real timestamp -
        // a fixed value keeps repeated `dotnet ef database update` runs
        // against a fresh database byte-for-byte identical.
        private static readonly DateTime SeedTimestamp = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        private static Guid DeterministicId(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }

        private static Guid PermissionId(string code) => DeterministicId($"admin_permission:{code}");

        private static Guid RoleId(string roleName) => DeterministicId($"admin_role:{roleName}");

        // Frozen to the SRS section 12 modules that existed when this migration
        // was authored (task 96a). AdminModules.All and AdminPermissionCatalog
        // keep growing as later tasks add modules (Partner/Payout via task 150c,
        // Referral via 173, Chat via 194, Subscription via 180, NestlyCoins via
        // 202) - each of those ships its own incremental "SeedXPermissions"
        // migration that inserts only its own module's rows. Reading
        // AdminPermissionCatalog.Permissions here without this filter computes
        // against whatever AdminModules.All contains in the CURRENTLY COMPILED
        // code, not what existed on 2026-07-31 - on a fresh database that
        // silently re-seeds every later module's rows too, colliding with each
        // of those migrations' own INSERTs on their primary keys. This is a
        // real bug fix, not a style preference: without it, `dotnet ef database
        // update` cannot succeed from an empty database at all.
        private static readonly string[] OriginalModules =
        [
            AdminModules.Dashboard, AdminModules.Customers, AdminModules.Catalog,
            AdminModules.Pricing, AdminModules.Serviceability, AdminModules.Slots,
            AdminModules.Bookings, AdminModules.Coupons, AdminModules.Support,
            AdminModules.Reviews, AdminModules.Cms, AdminModules.Notifications,
            AdminModules.Reports, AdminModules.Audit, AdminModules.Settings
        ];

        // MigrationBuilder.InsertData's multi-row overload takes a
        // rectangular object[,], not a jagged object[][] - this converts the
        // more readable row-by-row LINQ projections below into that shape.
        private static object[,] AsRows(IReadOnlyList<object[]> rows)
        {
            var result = new object[rows.Count, rows[0].Length];
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < rows[i].Length; j++)
                {
                    result[i, j] = rows[i][j];
                }
            }

            return result;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "admin_user",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_user_role_id",
                table: "admin_user",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_admin_user_admin_role_role_id",
                table: "admin_user",
                column: "role_id",
                principalTable: "admin_role",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            var originalPermissions = AdminPermissionCatalog.Permissions
                .Where(p => OriginalModules.Contains(p.Module))
                .ToList();

            migrationBuilder.InsertData(
                table: "admin_permission",
                columns: new[] { "id", "code", "module", "description", "created_at" },
                values: AsRows(originalPermissions
                    .Select(p => new object[] { PermissionId(p.Code), p.Code, p.Module, p.Description, SeedTimestamp })
                    .ToArray()));

            migrationBuilder.InsertData(
                table: "admin_role",
                columns: new[] { "id", "name", "description", "created_at", "updated_at" },
                values: AsRows(AdminRoleNames.All
                    .Select(role => new object[] { RoleId(role), role, AdminRoleNames.Descriptions[role], SeedTimestamp, SeedTimestamp })
                    .ToArray()));

            migrationBuilder.InsertData(
                table: "role_permission_mapping",
                columns: new[] { "id", "role_id", "permission_id", "created_at" },
                values: AsRows(AdminRoleNames.All
                    .SelectMany(role => AdminPermissionCatalog.RolePermissionCodes[role]
                        .Where(code => originalPermissions.Any(p => p.Code == code))
                        .Select(code => new object[]
                        {
                            DeterministicId($"role_permission_mapping:{role}:{code}"),
                            RoleId(role),
                            PermissionId(code),
                            SeedTimestamp
                        }))
                    .ToArray()));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_permission_mapping;");
            migrationBuilder.Sql("DELETE FROM admin_permission;");
            migrationBuilder.Sql("DELETE FROM admin_role;");

            migrationBuilder.DropForeignKey(
                name: "fk_admin_user_admin_role_role_id",
                table: "admin_user");

            migrationBuilder.DropIndex(
                name: "ix_admin_user_role_id",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "admin_user");
        }
    }
}
