using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceInclusionsExclusions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exclusions",
                table: "service",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "inclusions",
                table: "service",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exclusions",
                table: "service");

            migrationBuilder.DropColumn(
                name: "inclusions",
                table: "service");
        }
    }
}
