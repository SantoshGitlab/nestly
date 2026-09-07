using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 88h: seeds the ProviderKycApproved/ProviderKycRejected/ProviderActivated
    /// notification_template rows added to
    /// <see cref="NotificationTemplateSeedData.BuildDefaults"/> (9 rows: three
    /// event types x three channels). Same incremental-seed shape as
    /// 20260808011713_SeedProviderChangedNotificationTemplates.cs - only the
    /// new event types' rows are inserted; every other event type's rows
    /// already exist on a live database.
    ///
    /// Without this migration, an admin approving/rejecting a KYC document or
    /// activating a provider would record "no_template" failures on a live
    /// database rather than sending anything.
    ///
    /// Data-only: the model is unchanged, so the accompanying .Designer.cs
    /// snapshot is byte-identical to the preceding migration's.
    /// </summary>
    public partial class SeedProviderKycNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] columns =
            {
                "id", "event_type", "channel", "template_key", "subject", "body",
                "is_active", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id"
            };

            var seededEventTypes = new[]
            {
                NotificationEventType.ProviderKycApproved,
                NotificationEventType.ProviderKycRejected,
                NotificationEventType.ProviderActivated
            };

            foreach (var row in NotificationTemplateSeedData.BuildDefaults()
                .Where(r => seededEventTypes.Contains(r.EventType)))
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
                "DELETE FROM notification_template WHERE event_type IN ('ProviderKycApproved', 'ProviderKycRejected', 'ProviderActivated');");
        }
    }
}
