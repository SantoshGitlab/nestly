using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 295: seeds the ProviderChanged notification_template rows added to
    /// <see cref="NotificationTemplateSeedData.BuildDefaults"/> (3 rows: one
    /// event type x three channels). Same incremental-seed shape as
    /// 20260807104500_SeedFulfilmentNotificationTemplates.cs - only the new
    /// event type's rows are inserted; every other event type's rows already
    /// exist on a live database.
    ///
    /// Without this migration the "your professional has changed" dispatch
    /// would record "no_template" failures on a live database rather than
    /// sending anything, and the renderer's fallback is a logged failure, not
    /// a throw - so the loss would be silent from the caller's side.
    ///
    /// Data-only: the model is unchanged, so the accompanying .Designer.cs
    /// snapshot is byte-identical to the preceding migration's.
    /// </summary>
    public partial class SeedProviderChangedNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string[] columns =
            {
                "id", "event_type", "channel", "template_key", "subject", "body",
                "is_active", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id"
            };

            foreach (var row in NotificationTemplateSeedData.BuildDefaults()
                .Where(r => r.EventType == NotificationEventType.ProviderChanged))
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
            migrationBuilder.Sql("DELETE FROM notification_template WHERE event_type = 'ProviderChanged';");
        }
    }
}
