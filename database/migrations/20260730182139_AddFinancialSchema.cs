using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coupon",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    discount_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    max_discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    min_order_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valid_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    usage_limit_total = table.Column<int>(type: "integer", nullable: true),
                    usage_limit_per_customer = table.Column<int>(type: "integer", nullable: true),
                    redemption_count = table.Column<int>(type: "integer", nullable: false),
                    applicable_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_segment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon", x => x.id);
                    table.ForeignKey(
                        name: "fk_coupon_category_applicable_category_id",
                        column: x => x.applicable_category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wallet_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_wallet_ledger_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_transaction", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_transaction_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_transaction_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon_redemption",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    redeemed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon_redemption", x => x.id);
                    table.ForeignKey(
                        name: "fk_coupon_redemption_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coupon_redemption_coupon_coupon_id",
                        column: x => x.coupon_id,
                        principalTable: "coupon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coupon_redemption_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    gateway_order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gateway_payment_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_attempt", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_attempt_payment_transactions_payment_transaction_id",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refund_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway_refund_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_transaction", x => x.id);
                    table.ForeignKey(
                        name: "fk_refund_transaction_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_transaction_payment_transaction_payment_transaction_",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coupon_applicable_category_id",
                table: "coupon",
                column: "applicable_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_code",
                table: "coupon",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupon_valid_from_utc_valid_to_utc",
                table: "coupon",
                columns: new[] { "valid_from_utc", "valid_to_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemption_booking_id",
                table: "coupon_redemption",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemption_coupon_id_customer_id",
                table: "coupon_redemption",
                columns: new[] { "coupon_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemption_customer_id",
                table: "coupon_redemption",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempt_gateway_order_id",
                table: "payment_attempt",
                column: "gateway_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempt_payment_transaction_id_attempt_number",
                table: "payment_attempt",
                columns: new[] { "payment_transaction_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_transaction_booking_id",
                table: "payment_transaction",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_transaction_customer_id",
                table: "payment_transaction",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transaction_idempotency_key",
                table: "payment_transaction",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refund_transaction_booking_id",
                table: "refund_transaction",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_transaction_payment_transaction_id",
                table: "refund_transaction",
                column: "payment_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ledger_customer_id_created_at_utc",
                table: "wallet_ledger",
                columns: new[] { "customer_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coupon_redemption");

            migrationBuilder.DropTable(
                name: "payment_attempt");

            migrationBuilder.DropTable(
                name: "refund_transaction");

            migrationBuilder.DropTable(
                name: "wallet_ledger");

            migrationBuilder.DropTable(
                name: "coupon");

            migrationBuilder.DropTable(
                name: "payment_transaction");
        }
    }
}
