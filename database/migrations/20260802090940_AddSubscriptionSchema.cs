using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "subscription_discount_amount_snapshot",
                table: "booking",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "subscription_free_visit_applied",
                table: "booking",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "subscription_id",
                table: "booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_subscription",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    billing_cycle_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    free_visits_included_snapshot = table.Column<int>(type: "integer", nullable: false),
                    discount_percent_snapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    priority_slot_flag_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    free_visits_remaining = table.Column<int>(type: "integer", nullable: false),
                    next_billing_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_payment_failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    expiring_soon_notified_for_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_subscription", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_subscription_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    free_visits_included = table.Column<int>(type: "integer", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    priority_slot_flag = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plan", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscription_customer_id",
                table: "customer_subscription",
                column: "customer_id",
                unique: true,
                filter: "\"status\" IN ('Active', 'PaymentFailed')");

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscription_next_billing_date_utc",
                table: "customer_subscription",
                column: "next_billing_date_utc");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plan_is_active",
                table: "subscription_plan",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plan_name",
                table: "subscription_plan",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_subscription");

            migrationBuilder.DropTable(
                name: "subscription_plan");

            migrationBuilder.DropColumn(
                name: "subscription_discount_amount_snapshot",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "subscription_free_visit_applied",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "subscription_id",
                table: "booking");
        }
    }
}
