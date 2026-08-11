using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogHierarchyAndBookingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "service_addon",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_category_id",
                table: "category",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "service_variant_id",
                table: "booking_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "variant_duration_minutes_snapshot",
                table: "booking_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "variant_name_snapshot",
                table: "booking_item",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "add_on_group_id",
                table: "booking_addon_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_name_snapshot",
                table: "booking_addon_item",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_add_on_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    selection_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    min_select = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_select = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_add_on_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_variant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    inclusions_override = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_variant", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_addon_group_id",
                table: "service_addon",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_parent_category_id",
                table: "category",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_add_on_group_service_id",
                table: "service_add_on_group",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_variant_service_id",
                table: "service_variant",
                column: "service_id");

            migrationBuilder.AddForeignKey(
                name: "fk_category_category_parent_category_id",
                table: "category",
                column: "parent_category_id",
                principalTable: "category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_service_addon_service_add_on_group_group_id",
                table: "service_addon",
                column: "group_id",
                principalTable: "service_add_on_group",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_category_category_parent_category_id",
                table: "category");

            migrationBuilder.DropForeignKey(
                name: "fk_service_addon_service_add_on_group_group_id",
                table: "service_addon");

            migrationBuilder.DropTable(
                name: "service_add_on_group");

            migrationBuilder.DropTable(
                name: "service_variant");

            migrationBuilder.DropIndex(
                name: "ix_service_addon_group_id",
                table: "service_addon");

            migrationBuilder.DropIndex(
                name: "ix_category_parent_category_id",
                table: "category");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "service_addon");

            migrationBuilder.DropColumn(
                name: "parent_category_id",
                table: "category");

            migrationBuilder.DropColumn(
                name: "service_variant_id",
                table: "booking_item");

            migrationBuilder.DropColumn(
                name: "variant_duration_minutes_snapshot",
                table: "booking_item");

            migrationBuilder.DropColumn(
                name: "variant_name_snapshot",
                table: "booking_item");

            migrationBuilder.DropColumn(
                name: "add_on_group_id",
                table: "booking_addon_item");

            migrationBuilder.DropColumn(
                name: "group_name_snapshot",
                table: "booking_addon_item");
        }
    }
}
