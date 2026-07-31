using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Nestly.Domain;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Tasks 131a-131h: the admin-configurable settings/feature-flag store
    /// (SRS 12.19). Seeds one row per <see cref="SystemSettingGroups"/> with
    /// its default value so <c>ISystemSettingsService</c> always finds a
    /// group to read/update - admins edit an existing group, they never
    /// create one. Ids are deterministic (<see cref="DeterministicId"/>),
    /// same reasoning as <c>AddAdminPermissionMatrix</c>: a fresh database
    /// gets byte-for-byte identical seed rows every time this migration runs.
    /// </summary>
    public partial class AddSystemSettings : Migration
    {
        // Fixed rather than DateTime.UtcNow, same reasoning as
        // AddAdminPermissionMatrix.SeedTimestamp: these are reference/default
        // rows, not a real event, so re-running Up() against a fresh database
        // must produce identical output every time.
        private static readonly DateTime SeedTimestamp = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        private static Guid DeterministicId(string seed)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }

        private static Guid GroupId(string groupKey) => DeterministicId($"system_setting:{groupKey}");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_setting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_setting", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_setting_group_key",
                table: "system_setting",
                column: "group_key",
                unique: true);

            // Default value per group, as camelCase JSON matching the
            // JsonSerializerDefaults.Web options SystemSettingsService uses
            // to (de)serialize. Numeric defaults mirror the existing
            // appsettings.json-bound CancellationPolicyOptions/
            // ReschedulePolicyOptions where a group corresponds to one, so
            // the admin-visible default matches current runtime behavior.
            string[] columns = { "id", "group_key", "value_json", "created_at_utc", "updated_at_utc", "updated_by_admin_user_id" };

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Booking),
                    SystemSettingGroups.Booking,
                    "{\"minLeadTimeHours\":2,\"maxAdvanceBookingDays\":30,\"maxActiveBookingsPerCustomer\":null,\"allowSameDayBooking\":true}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Slot),
                    SystemSettingGroups.Slot,
                    "{\"defaultSlotDurationMinutes\":60,\"sameDayCutoffHours\":2,\"maxAdvanceBookingDays\":30,\"defaultSlotCapacity\":1,\"allowOverbooking\":false}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Cancellation),
                    SystemSettingGroups.Cancellation,
                    "{\"freeCancellationWindowHours\":4,\"lateCancellationFeePercentage\":20,\"allowAdminOverride\":true}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Reschedule),
                    SystemSettingGroups.Reschedule,
                    "{\"minHoursBeforeSlot\":2,\"maxReschedulesPerBooking\":2,\"lateFeeThresholdHours\":6,\"lateRescheduleFeePercentage\":10}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Tax),
                    SystemSettingGroups.Tax,
                    "{\"defaultTaxPercentage\":18,\"taxRegistrationNumber\":null,\"taxInclusivePricing\":false}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Wallet),
                    SystemSettingGroups.Wallet,
                    "{\"maxWalletBalance\":50000,\"maxWalletUsagePercentagePerBooking\":100,\"walletCreditExpiryDays\":null,\"allowWalletTopUp\":true}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });

            migrationBuilder.InsertData(
                table: "system_setting",
                columns: columns,
                values: new object[]
                {
                    GroupId(SystemSettingGroups.Coupon),
                    SystemSettingGroups.Coupon,
                    "{\"maxDiscountPercentagePerCoupon\":50,\"maxActiveCouponsPerCustomer\":null,\"allowCouponStacking\":false,\"couponsEnabled\":true}",
                    SeedTimestamp,
                    SeedTimestamp,
                    null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_setting");
        }
    }
}
