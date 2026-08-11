using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "service_group_id",
                table: "service",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_group", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_service_group_id",
                table: "service",
                column: "service_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_group_category_id",
                table: "service_group",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_service_service_group_service_group_id",
                table: "service",
                column: "service_group_id",
                principalTable: "service_group",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_service_service_group_service_group_id",
                table: "service");

            migrationBuilder.DropTable(
                name: "service_group");

            migrationBuilder.DropIndex(
                name: "ix_service_service_group_id",
                table: "service");

            migrationBuilder.DropColumn(
                name: "service_group_id",
                table: "service");
        }
    }
}
