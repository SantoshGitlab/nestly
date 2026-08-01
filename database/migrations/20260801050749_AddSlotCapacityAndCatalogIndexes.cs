using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotCapacityAndCatalogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_category_id",
                table: "service");

            migrationBuilder.CreateTable(
                name: "slot_booking_counter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    booked_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slot_booking_counter", x => x.id);
                    table.ForeignKey(
                        name: "fk_slot_booking_counter_slot_windows_slot_window_id",
                        column: x => x.slot_window_id,
                        principalTable: "slot_window",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_category_id_is_active",
                table: "service",
                columns: new[] { "category_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_slot_booking_counter_slot_window_id_slot_date",
                table: "slot_booking_counter",
                columns: new[] { "slot_window_id", "slot_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slot_booking_counter");

            migrationBuilder.DropIndex(
                name: "ix_service_category_id_is_active",
                table: "service");

            migrationBuilder.CreateIndex(
                name: "ix_service_category_id",
                table: "service",
                column: "category_id");
        }
    }
}
