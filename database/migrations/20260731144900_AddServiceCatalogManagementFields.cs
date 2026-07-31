using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_mandatory",
                table: "service_addon",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_quantity_allowed",
                table: "service_addon",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "service",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "is_add_on_allowed",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_address_required",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_customer_note_allowed",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_featured",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_inspection_based",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_quantity_allowed",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_slot_required",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_tax_applicable",
                table: "service",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing_type",
                table: "service",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed");

            migrationBuilder.AddColumn<string>(
                name: "seo_meta_description",
                table: "service",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title",
                table: "service",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_description",
                table: "service",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "service",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_mandatory",
                table: "service_addon");

            migrationBuilder.DropColumn(
                name: "is_quantity_allowed",
                table: "service_addon");

            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_add_on_allowed",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_address_required",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_customer_note_allowed",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_featured",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_inspection_based",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_quantity_allowed",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_slot_required",
                table: "service");

            migrationBuilder.DropColumn(
                name: "is_tax_applicable",
                table: "service");

            migrationBuilder.DropColumn(
                name: "pricing_type",
                table: "service");

            migrationBuilder.DropColumn(
                name: "seo_meta_description",
                table: "service");

            migrationBuilder.DropColumn(
                name: "seo_title",
                table: "service");

            migrationBuilder.DropColumn(
                name: "short_description",
                table: "service");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "service");
        }
    }
}
