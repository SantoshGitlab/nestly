using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Tasks 126a-d (SRS 12.17): the admin-editable notification template
    /// store. Seeds every (EventType, Channel) row
    /// <c>NotificationTemplateRenderer</c> used to hard-code as a fixed
    /// built-in dictionary before this migration, sourced from
    /// <see cref="NotificationTemplateSeedData"/> - the single place that
    /// content now lives, also reused by <c>NotificationTemplateRendererTests</c>
    /// so a fresh database and the test suite can never silently drift apart.
    /// Ids and the timestamp are deterministic (<see cref="NotificationTemplateSeedData.DeterministicId"/>),
    /// same reasoning as <c>AddSystemSettings.DeterministicId</c>/<c>SeedTimestamp</c>.
    /// </summary>
    public partial class AddNotificationTemplateManagement : Migration
    {
        // Frozen to the 8 event types task 87a-d's trigger wiring depended on
        // when this migration was authored (see NotificationTemplateSeedData's
        // own doc comment). NotificationTemplateSeedData.BuildDefaults() keeps
        // growing as later tasks add event types (RecurringBooking via task
        // 188, Referral via 172, Subscription via 183) - each ships its own
        // incremental "SeedXNotificationTemplates"/filtered migration that
        // inserts only its own new event types. Calling BuildDefaults() here
        // without this filter reads the CURRENTLY COMPILED seed data, not what
        // existed on 2026-07-31 - on a fresh database that silently re-seeds
        // every later event type's rows too, colliding with each of those
        // migrations' own INSERTs on the unique (event_type, channel) index.
        private static readonly NotificationEventType[] OriginalEventTypes =
        [
            NotificationEventType.Welcome, NotificationEventType.BookingConfirmed,
            NotificationEventType.PaymentSuccess, NotificationEventType.PaymentFailed,
            NotificationEventType.BookingCancelled, NotificationEventType.BookingRescheduled,
            NotificationEventType.RefundProcessed, NotificationEventType.SupportTicketUpdate
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_template",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_template", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_event_type_channel",
                table: "notification_template",
                columns: new[] { "event_type", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_template_key",
                table: "notification_template",
                column: "template_key",
                unique: true);

            string[] columns =
            {
                "id", "event_type", "channel", "template_key", "subject", "body",
                "is_active", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id"
            };

            foreach (var row in NotificationTemplateSeedData.BuildDefaults().Where(r => OriginalEventTypes.Contains(r.EventType)))
            {
                migrationBuilder.InsertData(
                    table: "notification_template",
                    columns: columns,
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
            migrationBuilder.DropTable(
                name: "notification_template");
        }
    }
}
