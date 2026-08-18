using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAmcSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "amc_contract_id",
                table: "booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "amc_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    visits_included = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_amc_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_amc_plan_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_amc_contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category_id_snapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    term_months_snapshot = table.Column<int>(type: "integer", nullable: false),
                    visits_included_snapshot = table.Column<int>(type: "integer", nullable: false),
                    asset_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    visits_remaining = table.Column<int>(type: "integer", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expiring_soon_notified_for_end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_amc_contract", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_amc_contract_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_amc_contract_payment_transactions_payment_transact",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "amc_service_visit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_amc_service_visit", x => x.id);
                    table.ForeignKey(
                        name: "fk_amc_service_visit_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_amc_service_visit_customer_amc_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "customer_amc_contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_amc_contract_id",
                table: "booking",
                column: "amc_contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_amc_plan_category_id",
                table: "amc_plan",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_amc_plan_name",
                table: "amc_plan",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_amc_service_visit_booking_id",
                table: "amc_service_visit",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_amc_service_visit_contract_id",
                table: "amc_service_visit",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_amc_contract_customer_id",
                table: "customer_amc_contract",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_amc_contract_end_date_utc",
                table: "customer_amc_contract",
                column: "end_date_utc");

            migrationBuilder.CreateIndex(
                name: "ix_customer_amc_contract_payment_transaction_id",
                table: "customer_amc_contract",
                column: "payment_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_amc_contract_status",
                table: "customer_amc_contract",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_customer_amc_contracts_amc_contract_id",
                table: "booking",
                column: "amc_contract_id",
                principalTable: "customer_amc_contract",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_customer_amc_contracts_amc_contract_id",
                table: "booking");

            migrationBuilder.DropTable(
                name: "amc_plan");

            migrationBuilder.DropTable(
                name: "amc_service_visit");

            migrationBuilder.DropTable(
                name: "customer_amc_contract");

            migrationBuilder.DropIndex(
                name: "ix_booking_amc_contract_id",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "amc_contract_id",
                table: "booking");
        }
    }
}
