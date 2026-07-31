using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotAvailabilityOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "slot_availability_override",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_window_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slot_availability_override", x => x.id);
                    table.ForeignKey(
                        name: "fk_slot_availability_override_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_slot_availability_override_city_city_id",
                        column: x => x.city_id,
                        principalTable: "city",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_slot_availability_override_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_slot_availability_override_slot_windows_slot_window_id",
                        column: x => x.slot_window_id,
                        principalTable: "slot_window",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_slot_availability_override_category_id",
                table: "slot_availability_override",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_slot_availability_override_city_id_date",
                table: "slot_availability_override",
                columns: new[] { "city_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_slot_availability_override_service_id",
                table: "slot_availability_override",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_slot_availability_override_slot_window_id",
                table: "slot_availability_override",
                column: "slot_window_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slot_availability_override");
        }
    }
}
