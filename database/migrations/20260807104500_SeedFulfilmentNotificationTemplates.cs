using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 276: seeds the five fulfilment-lifecycle notification_template
    /// rows added to <see cref="NotificationTemplateSeedData.BuildDefaults"/>
    /// - ProviderAssigned, ProviderEnRoute, ProviderArrived, JobStarted,
    /// JobCompleted (15 rows: 5 event types x 3 channels). Same incremental-seed
    /// shape as 20260805181625_SeedBookingExpiredNotificationTemplates.cs -
    /// only the new event types' rows are inserted; every other event type's
    /// rows already exist on a live database.
    ///
    /// Without this migration BookingNotificationTriggerHandler's new dispatch
    /// calls would record "no_template" failures on a live database instead of
    /// sending anything - the renderer falls back to a logged failure rather
    /// than throwing, so the loss would be silent from the caller's side.
    ///
    /// Data-only: the model is unchanged, so the accompanying .Designer.cs
    /// snapshot is byte-identical to the preceding migration's.
    /// </summary>
    public partial class SeedFulfilmentNotificationTemplates : Migration
    {
        private static readonly NotificationEventType[] NewEventTypes =
        [
            NotificationEventType.ProviderAssigned,
            NotificationEventType.ProviderEnRoute,
            NotificationEventType.ProviderArrived,
            NotificationEventType.JobStarted,
            NotificationEventType.JobCompleted
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] columns =
            {
                "id", "event_type", "channel", "template_key", "subject", "body",
                "is_active", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id"
            };

            foreach (var row in NotificationTemplateSeedData.BuildDefaults().Where(r => NewEventTypes.Contains(r.EventType)))
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
            migrationBuilder.Sql(
                "DELETE FROM notification_template WHERE event_type IN ('ProviderAssigned', 'ProviderEnRoute', 'ProviderArrived', 'JobStarted', 'JobCompleted');");
        }
    }
}
