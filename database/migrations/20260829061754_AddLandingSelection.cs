using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "landing_selection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_type = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_landing_selection", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_landing_selection_section_type_category_id_sort_order",
                table: "landing_selection",
                columns: new[] { "section_type", "category_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_landing_selection_section_type_sort_order",
                table: "landing_selection",
                columns: new[] { "section_type", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landing_selection");
        }
    }
}
