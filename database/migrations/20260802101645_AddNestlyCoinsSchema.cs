using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNestlyCoinsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nestly_coins_program_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    earn_rate_per_100 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    minimum_order_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    require_reorder = table.Column<bool>(type: "boolean", nullable: false),
                    max_coins_per_month = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    expiry_days = table.Column<int>(type: "integer", nullable: false),
                    clawback_window_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nestly_coins_program_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nestly_coins_program_config_audience",
                table: "nestly_coins_program_config",
                column: "audience",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nestly_coins_program_config");
        }
    }
}
