using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_group_id",
                table: "category",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "category_group",
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
                    table.PrimaryKey("pk_category_group", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_category_category_group_id",
                table: "category",
                column: "category_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_group_category_id",
                table: "category_group",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_category_category_group_category_group_id",
                table: "category",
                column: "category_group_id",
                principalTable: "category_group",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_category_category_group_category_group_id",
                table: "category");

            migrationBuilder.DropTable(
                name: "category_group");

            migrationBuilder.DropIndex(
                name: "ix_category_category_group_id",
                table: "category");

            migrationBuilder.DropColumn(
                name: "category_group_id",
                table: "category");
        }
    }
}
