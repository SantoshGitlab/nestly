using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderLocationPing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "location_updated_at_utc",
                table: "provider",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "provider_location_ping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    accuracy_metres = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_location_ping", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_location_ping_booking_id_recorded_at_utc",
                table: "provider_location_ping",
                columns: new[] { "booking_id", "recorded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_location_ping_provider_id_recorded_at_utc",
                table: "provider_location_ping",
                columns: new[] { "provider_id", "recorded_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_location_ping");

            migrationBuilder.DropColumn(
                name: "location_updated_at_utc",
                table: "provider");
        }
    }
}
