using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingSupportBookingFinancialSchemaDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author_type",
                table: "support_ticket_comment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "commission_amount",
                table: "payment_transaction",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate_percentage",
                table: "payment_transaction",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_cancellation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    within_free_cancellation_window = table.Column<bool>(type: "boolean", nullable: false),
                    cancellation_fee_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    refund_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    refund_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    internal_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_cancellation", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_cancellation_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_reschedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    from_slot_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_slot_start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    from_slot_end_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    to_slot_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_slot_start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    to_slot_end_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    is_late = table.Column<bool>(type: "boolean", nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_reschedule", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_reschedule_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_token_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_escrow_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    commission_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_escrow_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_platform_escrow_ledger_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    review_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    issue_tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_ticket",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolution_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_disputed = table.Column<bool>(type: "boolean", nullable: false),
                    dispute_outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    dispute_resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_ticket", x => x.id);
                    table.ForeignKey(
                        name: "fk_support_ticket_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_support_ticket_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    support_ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_event_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_event_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_event_support_tickets_support_ticket_id",
                        column: x => x.support_ticket_id,
                        principalTable: "support_ticket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_comment_support_ticket_id",
                table: "support_ticket_comment",
                column: "support_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_cancellation_booking_id",
                table: "booking_cancellation",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_reschedule_booking_id",
                table: "booking_reschedule",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_token_customer_id_is_active",
                table: "device_token",
                columns: new[] { "customer_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_device_token_token",
                table: "device_token",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_booking_id",
                table: "notification_event",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_customer_id",
                table: "notification_event",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_event_type_status",
                table: "notification_event",
                columns: new[] { "event_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_support_ticket_id",
                table: "notification_event",
                column: "support_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_escrow_ledger_booking_id_created_at_utc",
                table: "platform_escrow_ledger",
                columns: new[] { "booking_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_escrow_ledger_created_at_utc",
                table: "platform_escrow_ledger",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_review_booking_id",
                table: "review",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_customer_id",
                table: "review",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_service_id",
                table: "review",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_booking_id",
                table: "support_ticket",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_customer_id",
                table: "support_ticket",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_status",
                table: "support_ticket",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_support_ticket_comment_support_tickets_support_ticket_id",
                table: "support_ticket_comment",
                column: "support_ticket_id",
                principalTable: "support_ticket",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_support_ticket_comment_support_tickets_support_ticket_id",
                table: "support_ticket_comment");

            migrationBuilder.DropTable(
                name: "booking_cancellation");

            migrationBuilder.DropTable(
                name: "booking_reschedule");

            migrationBuilder.DropTable(
                name: "device_token");

            migrationBuilder.DropTable(
                name: "notification_event");

            migrationBuilder.DropTable(
                name: "platform_escrow_ledger");

            migrationBuilder.DropTable(
                name: "review");

            migrationBuilder.DropTable(
                name: "support_ticket");

            migrationBuilder.DropIndex(
                name: "ix_support_ticket_comment_support_ticket_id",
                table: "support_ticket_comment");

            migrationBuilder.DropColumn(
                name: "author_type",
                table: "support_ticket_comment");

            migrationBuilder.DropColumn(
                name: "commission_amount",
                table: "payment_transaction");

            migrationBuilder.DropColumn(
                name: "commission_rate_percentage",
                table: "payment_transaction");
        }
    }
}
