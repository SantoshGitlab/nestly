using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 202: seeds the new NestlyCoins permission module
    /// docs/NESTLY-COINS.md's RBAC ADDITIONS calls for, added to
    /// <see cref="AdminModules.All"/> alongside this migration. Same
    /// incremental-seed shape as 20260801150655_SeedReferralPermissions.cs -
    /// only inserts the new module's rows; every other module's rows already
    /// exist from 96a (AddAdminPermissionMatrix) or the follow-up seeds since.
    /// </summary>
    public partial class SeedNestlyCoinsPermissions : Migration
    {
        // Distinct fixed timestamp from every other seed migration's, same
        // "reference data, not a real event" rationale.
        private static readonly DateTime SeedTimestamp = new(2026, 8, 2, 11, 19, 0, DateTimeKind.Utc);

        private static readonly string[] NewModules = [AdminModules.NestlyCoins];

        private static Guid DeterministicId(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }

        private static Guid PermissionId(string code) => DeterministicId($"admin_permission:{code}");

        private static Guid RoleId(string roleName) => DeterministicId($"admin_role:{roleName}");

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
            var newPermissions = AdminPermissionCatalog.Permissions
                .Where(p => NewModules.Contains(p.Module))
                .ToList();

            migrationBuilder.InsertData(
                table: "admin_permission",
                columns: new[] { "id", "code", "module", "description", "created_at" },
                values: AsRows(newPermissions
                    .Select(p => new object[] { PermissionId(p.Code), p.Code, p.Module, p.Description, SeedTimestamp })
                    .ToArray()));

            var newRoleGrants = AdminRoleNames.All
                .SelectMany(role => AdminPermissionCatalog.RolePermissionCodes[role]
                    .Where(code => newPermissions.Any(p => p.Code == code))
                    .Select(code => new object[]
                    {
                        DeterministicId($"role_permission_mapping:{role}:{code}"),
                        RoleId(role),
                        PermissionId(code),
                        SeedTimestamp
                    }))
                .ToList();

            migrationBuilder.InsertData(
                table: "role_permission_mapping",
                columns: new[] { "id", "role_id", "permission_id", "created_at" },
                values: AsRows(newRoleGrants));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM role_permission_mapping WHERE permission_id IN " +
                "(SELECT id FROM admin_permission WHERE module = 'nestly-coins');");
            migrationBuilder.Sql("DELETE FROM admin_permission WHERE module = 'nestly-coins';");
        }
    }
}
