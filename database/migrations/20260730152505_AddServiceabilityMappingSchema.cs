using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceabilityMappingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_city_mapping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_city_mapping", x => x.id);
                    table.ForeignKey(
                        name: "fk_category_city_mapping_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_category_city_mapping_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "city",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_pincode_mapping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pincode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_pincode_mapping", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_pincode_mapping_pincode_pincode_id",
                        column: x => x.pincode_id,
                        principalTable: "pincode",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_pincode_mapping_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_category_city_mapping_category_id_city_id",
                table: "category_city_mapping",
                columns: new[] { "category_id", "city_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_category_city_mapping_city_id",
                table: "category_city_mapping",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_pincode_mapping_pincode_id",
                table: "service_pincode_mapping",
                column: "pincode_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_pincode_mapping_service_id_pincode_id",
                table: "service_pincode_mapping",
                columns: new[] { "service_id", "pincode_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_city_mapping");

            migrationBuilder.DropTable(
                name: "service_pincode_mapping");
        }
    }
}
