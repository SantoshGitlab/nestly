using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Adds <c>FeatureFlagSettings.AutoManageServiceabilityEnabled</c> (SRS
    /// 12.19 "Feature flags" - docs/OPEN-FIXES-FEATURES.csv "Service to
    /// pincode mapping" follow-up: the admin kill switch for the whole
    /// auto-enable/auto-disable safety net). No schema change - <c>features</c>
    /// is an existing row in <c>system_setting</c> (see
    /// <c>AddFeatureFlagSettings</c>) whose <c>value_json</c> just gains one
    /// more key, defaulted to <c>true</c> so nothing about the already-live
    /// auto-management behaviour silently disables itself on this deploy.
    /// A plain <c>jsonb</c> merge/key-drop rather than <c>InsertData</c>
    /// (there is no new row to insert) - the same reasoning
    /// <c>AddFeatureFlagSettings</c> used for the "features" row's own
    /// insert, one level down: touch only the field this migration is about.
    /// </summary>
    public partial class AddAutoManageServiceabilityFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE system_setting " +
                "SET value_json = value_json || '{\"autoManageServiceabilityEnabled\":true}'::jsonb " +
                "WHERE group_key = 'features';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE system_setting " +
                "SET value_json = value_json - 'autoManageServiceabilityEnabled' " +
                "WHERE group_key = 'features';");
        }
    }
}
