using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenEscrowAndCouponPerCustomerCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coupon_customer_redemption_counter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserved_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon_customer_redemption_counter", x => x.id);
                    table.ForeignKey(
                        name: "fk_coupon_customer_redemption_counter_coupon_coupon_id",
                        column: x => x.coupon_id,
                        principalTable: "coupon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_coupon_customer_redemption_counter_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_escrow_ledger_source_reference_id",
                table: "platform_escrow_ledger",
                column: "source_reference_id",
                unique: true,
                filter: "entry_type = 'Hold'");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_customer_redemption_counter_coupon_id_customer_id",
                table: "coupon_customer_redemption_counter",
                columns: new[] { "coupon_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupon_customer_redemption_counter_customer_id",
                table: "coupon_customer_redemption_counter",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coupon_customer_redemption_counter");

            migrationBuilder.DropIndex(
                name: "ix_platform_escrow_ledger_source_reference_id",
                table: "platform_escrow_ledger");
        }
    }
}
