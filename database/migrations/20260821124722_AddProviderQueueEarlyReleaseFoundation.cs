using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderQueueEarlyReleaseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_duration_based",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "booking_provider_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_duration_based_snapshot",
                table: "booking",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "service_duration_minutes_snapshot",
                table: "booking",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_duration_based",
                table: "service");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "booking_provider_assignment");

            migrationBuilder.DropColumn(
                name: "is_duration_based_snapshot",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "service_duration_minutes_snapshot",
                table: "booking");
        }
    }
}
