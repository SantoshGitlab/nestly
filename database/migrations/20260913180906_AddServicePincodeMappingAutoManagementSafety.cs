using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the auto-enable/auto-disable safety-net columns to
    /// <c>service_pincode_mapping</c> (docs/OPEN-FIXES-FEATURES.csv
    /// "Service to pincode mapping" follow-up): <c>is_pinned</c> (admin
    /// override, defaults false), <c>last_auto_toggled_at_utc</c> (flap-
    /// protection cooldown clock) and <c>pending_auto_disable_since</c>
    /// (auto-disable grace-period timer). See
    /// <see cref="Nestly.Domain.ServicePincodeMapping"/> for the full
    /// behaviour these back.
    /// </summary>
    public partial class AddServicePincodeMappingAutoManagementSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "service_pincode_mapping",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_auto_toggled_at_utc",
                table: "service_pincode_mapping",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pending_auto_disable_since",
                table: "service_pincode_mapping",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "service_pincode_mapping");

            migrationBuilder.DropColumn(
                name: "last_auto_toggled_at_utc",
                table: "service_pincode_mapping");

            migrationBuilder.DropColumn(
                name: "pending_auto_disable_since",
                table: "service_pincode_mapping");
        }
    }
}
