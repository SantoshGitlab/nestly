using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_end_date",
                table: "service_city_price",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_start_date",
                table: "service_city_price",
                type: "date",
                nullable: false,
                defaultValueSql: "CURRENT_DATE");

            migrationBuilder.CreateTable(
                name: "promotional_price",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discounted_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotional_price", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotional_price_city_city_id",
                        column: x => x.city_id,
                        principalTable: "city",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promotional_price_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_promotional_price_city_id",
                table: "promotional_price",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_promotional_price_service_id_city_id",
                table: "promotional_price",
                columns: new[] { "service_id", "city_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotional_price");

            migrationBuilder.DropColumn(
                name: "effective_end_date",
                table: "service_city_price");

            migrationBuilder.DropColumn(
                name: "effective_start_date",
                table: "service_city_price");
        }
    }
}
