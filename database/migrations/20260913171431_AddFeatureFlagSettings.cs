using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the "features" settings group (SRS 12.19 "Feature flags") -
    /// customer- and provider-facing feature toggles, admin-managed the same
    /// way as every other group added in <c>AddSystemSettings</c>. No schema
    /// change: <c>system_setting</c> already exists, this only seeds its
    /// eighth row so <c>ISystemSettingsService.GetFeatureFlagSettingsAsync</c>
    /// always finds a row to read/update. Every flag defaults to
    /// <c>true</c> - nothing customer- or provider-facing silently disappears
    /// on first deploy of this group.
    /// </summary>
    public partial class AddFeatureFlagSettings : Migration
    {
        // Fixed rather than DateTime.UtcNow, same reasoning as
        // AddSystemSettings.SeedTimestamp: a reference/default row, not a
        // real event, so re-running Up() against a fresh database produces
        // identical output every time.
        private static readonly DateTime SeedTimestamp = new(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);

        private static Guid DeterministicId(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }

        private static Guid GroupId(string groupKey) => DeterministicId($"system_setting:{groupKey}");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "system_setting",
                columns: new[] { "id", "group_key", "value_json", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id" },
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Feature),
                    SystemSettingGroups.Feature,
                    "{\"walletEnabled\":true,\"referralsEnabled\":true,\"amcSubscriptionsEnabled\":true,\"serviceRatingsEnabled\":true,\"bookingHelpLinkEnabled\":true,\"ratingsPageEnabled\":true,\"calendarViewEnabled\":true,\"earningsLedgerEnabled\":true,\"offersScreenEnabled\":true}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_setting",
                keyColumn: "id",
                keyValue: GroupId(SystemSettingGroups.Feature));
        }
    }
}
