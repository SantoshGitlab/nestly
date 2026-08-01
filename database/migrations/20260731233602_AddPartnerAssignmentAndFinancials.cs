using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerAssignmentAndFinancials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_partner_id",
                table: "booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_partner_assignment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_partner_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_partner_assignment_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_partner_assignment_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_background_check",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    checked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_background_check", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_background_check_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_earning_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_partner_earning_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_earning_ledger_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_payout",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payout_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_payout", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_payout_partner_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_assigned_partner_id",
                table: "booking",
                column: "assigned_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_partner_assignment_booking_id_status",
                table: "booking_partner_assignment",
                columns: new[] { "booking_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_partner_assignment_partner_id",
                table: "booking_partner_assignment",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_partner_background_check_partner_id_checked_at",
                table: "partner_background_check",
                columns: new[] { "partner_id", "checked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_earning_ledger_partner_id_created_at_utc",
                table: "partner_earning_ledger",
                columns: new[] { "partner_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_payout_partner_id_period_start_period_end",
                table: "partner_payout",
                columns: new[] { "partner_id", "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_payout_status",
                table: "partner_payout",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_partners_assigned_partner_id",
                table: "booking",
                column: "assigned_partner_id",
                principalTable: "partner",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_partners_assigned_partner_id",
                table: "booking");

            migrationBuilder.DropTable(
                name: "booking_partner_assignment");

            migrationBuilder.DropTable(
                name: "partner_background_check");

            migrationBuilder.DropTable(
                name: "partner_earning_ledger");

            migrationBuilder.DropTable(
                name: "partner_payout");

            migrationBuilder.DropIndex(
                name: "ix_booking_assigned_partner_id",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "assigned_partner_id",
                table: "booking");
        }
    }
}
