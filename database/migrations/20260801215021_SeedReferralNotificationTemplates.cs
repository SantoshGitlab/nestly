using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 172: seeds the ReferralRegistered/ReferralRewardCredited
    /// notification_template rows added to
    /// <see cref="NotificationTemplateSeedData.BuildDefaults"/> for this
    /// phase. Same incremental-seed shape as
    /// 20260801150655_SeedReferralPermissions.cs - only inserts the two new
    /// event types' rows (6 total: 2 event types x 3 channels); every other
    /// event type's rows already exist from
    /// 20260731152427_AddNotificationTemplateManagement. The dispatch call
    /// sites for both events already existed (tasks 161/163/165,
    /// CustomerRegistrationService and ReferralRewardService) - without this
    /// migration they were silently recording "no_template" failures on a
    /// live database.
    /// </summary>
    public partial class SeedReferralNotificationTemplates : Migration
    {
        private static readonly NotificationEventType[] NewEventTypes =
        [
            NotificationEventType.ReferralRegistered,
            NotificationEventType.ReferralRewardCredited
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
                "DELETE FROM notification_template WHERE event_type IN ('ReferralRegistered', 'ReferralRewardCredited');");
        }
    }
}
