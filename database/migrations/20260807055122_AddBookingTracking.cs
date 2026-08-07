using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eta_seconds = table.Column<int>(type: "integer", nullable: true),
                    eta_distance_metres = table.Column<int>(type: "integer", nullable: true),
                    eta_computed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eta_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    eta_origin_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    eta_origin_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_tracking", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_tracking_booking_id",
                table: "booking_tracking",
                column: "booking_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_tracking");
        }
    }
}
