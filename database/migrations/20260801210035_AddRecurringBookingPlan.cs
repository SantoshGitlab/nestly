using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Tasks 184-185 (PRODUCT-ENHANCEMENTS.md section 2): recurring booking
    /// plans, their per-occurrence audit log, and the plan-level add-on
    /// selections a plan repeats onto every occurrence.
    ///
    /// Also inserts the two new <c>NotificationEventType</c> rows task 188
    /// adds (<c>RecurringBookingUpcoming</c>, <c>RecurringBookingSkipped</c>)
    /// into <c>notification_template</c> - the same seed source
    /// (<see cref="NotificationTemplateSeedData"/>) the original
    /// <c>AddNotificationTemplateManagement</c> migration seeded from, but run
    /// again here rather than editing that migration in place: a migration
    /// that already ran against this environment's database must never be
    /// edited after the fact (docs/DATABASE.md migration discipline)  - a new
    /// migration is how a database that already has the other rows picks up
    /// the two new ones, filtered to just the event types that did not exist
    /// yet at the time the original migration ran.
    /// </summary>
    public partial class AddRecurringBookingPlan : Migration
    {
        private static readonly NotificationEventType[] NewNotificationEventTypes =
        [
            NotificationEventType.RecurringBookingUpcoming,
            NotificationEventType.RecurringBookingSkipped
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_booking_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recurrence_day_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    recurrence_day_of_month = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    occurrence_count = table.Column<int>(type: "integer", nullable: true),
                    completed_occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    next_occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_booking_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_city_city_id",
                        column: x => x.city_id,
                        principalTable: "city",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_customer_address_address_id",
                        column: x => x.address_id,
                        principalTable: "customer_address",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_locality_locality_id",
                        column: x => x.locality_id,
                        principalTable: "locality",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_service_service_id",
                        column: x => x.service_id,
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_slot_windows_slot_window_id",
                        column: x => x.slot_window_id,
                        principalTable: "slot_window",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_booking_occurrence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurring_booking_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skip_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_booking_occurrence", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_booking_occurrence_recurring_booking_plans_recurr",
                        column: x => x.recurring_booking_plan_id,
                        principalTable: "recurring_booking_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_booking_plan_addon",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurring_booking_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    add_on_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_booking_plan_addon", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_booking_plan_addon_recurring_booking_plans_recurr",
                        column: x => x.recurring_booking_plan_id,
                        principalTable: "recurring_booking_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_occurrence_recurring_booking_plan_id_sche",
                table: "recurring_booking_occurrence",
                columns: new[] { "recurring_booking_plan_id", "scheduled_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_address_id",
                table: "recurring_booking_plan",
                column: "address_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_city_id",
                table: "recurring_booking_plan",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_customer_id",
                table: "recurring_booking_plan",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_locality_id",
                table: "recurring_booking_plan",
                column: "locality_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_service_id",
                table: "recurring_booking_plan",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_slot_window_id",
                table: "recurring_booking_plan",
                column: "slot_window_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_status_next_occurrence_date",
                table: "recurring_booking_plan",
                columns: new[] { "status", "next_occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_booking_plan_addon_recurring_booking_plan_id",
                table: "recurring_booking_plan_addon",
                column: "recurring_booking_plan_id");

            string[] notificationTemplateColumns =
            {
                "id", "event_type", "channel", "template_key", "subject", "body",
                "is_active", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id"
            };

            foreach (var row in NotificationTemplateSeedData.BuildDefaults().Where(r => NewNotificationEventTypes.Contains(r.EventType)))
            {
                migrationBuilder.InsertData(
                    table: "notification_template",
                    columns: notificationTemplateColumns,
                    values: new object[]
                    {
                        row.Id,
                        row.EventType.ToString(),
                        row.Channel.ToString(),
                        row.TemplateKey,
                        row.Subject,
                        row.Body,
                        true,
                        NotificationTemplateSeedData.SeedTimestampUtc,
                        NotificationTemplateSeedData.SeedTimestampUtc,
                        null
                    });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "notification_template",
                keyColumn: "event_type",
                keyValues: NewNotificationEventTypes.Select(t => (object)t.ToString()).ToArray());

            migrationBuilder.DropTable(
                name: "recurring_booking_occurrence");

            migrationBuilder.DropTable(
                name: "recurring_booking_plan_addon");

            migrationBuilder.DropTable(
                name: "recurring_booking_plan");
        }
    }
}
