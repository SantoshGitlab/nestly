using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 183: seeds the SubscriptionRenewed/SubscriptionExpiringSoon/
    /// SubscriptionPaymentFailed notification_template rows added to
    /// <see cref="NotificationTemplateSeedData.BuildDefaults"/> for this
    /// phase. Same incremental-seed shape as
    /// 20260801215021_SeedReferralNotificationTemplates.cs - only inserts
    /// the 3 new event types' rows (9 total: 3 event types x 3 channels);
    /// every other event type's rows already exist. Without this migration
    /// SubscriptionNotificationTriggerHandler's dispatch calls would
    /// silently record "no_template" failures on a live database.
    /// </summary>
    public partial class SeedSubscriptionNotificationTemplates : Migration
    {
        private static readonly NotificationEventType[] NewEventTypes =
        [
            NotificationEventType.SubscriptionRenewed,
            NotificationEventType.SubscriptionExpiringSoon,
            NotificationEventType.SubscriptionPaymentFailed
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
                "DELETE FROM notification_template WHERE event_type IN ('SubscriptionRenewed', 'SubscriptionExpiringSoon', 'SubscriptionPaymentFailed');");
        }
    }
}
