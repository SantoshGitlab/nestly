using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "referral_code",
                table: "customer",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referee_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_code_used = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    qualifying_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referrer_reward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referrer_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referee_reward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referee_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    min_qualifying_order_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referrer_wallet_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referrer_coupon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referee_wallet_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referee_coupon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    qualified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rewarded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_program_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_reward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referrer_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referee_reward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referee_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    min_qualifying_order_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referral_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    max_referrals_per_customer = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_program_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_referral_code",
                table: "customer",
                column: "referral_code",
                unique: true,
                filter: "referral_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_referral_referee_customer_id",
                table: "referral",
                column: "referee_customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_referrer_customer_id",
                table: "referral",
                column: "referrer_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_referral_status",
                table: "referral",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_referral_status_expires_at_utc",
                table: "referral",
                columns: new[] { "status", "expires_at_utc" });

            // Seed one default config row, same convention as
            // 20260731142533_AddSystemSettings.cs - SystemSettingsService's
            // GetGroupAsync returns a "not initialized" error rather than a
            // lazy default when a settings row is missing, so this table
            // needs at least one row for the referral flow to ever start.
            // Defaults are conservative placeholders admins are expected to
            // tune via task 167's config API before launching the program.
            migrationBuilder.InsertData(
                table: "referral_program_config",
                columns: new[]
                {
                    "id", "referrer_reward_type", "referrer_reward_value", "referee_reward_type",
                    "referee_reward_value", "min_qualifying_order_amount", "referral_expiry_days",
                    "max_referrals_per_customer", "is_active", "updated_at_utc", "updated_by_admin_user_id"
                },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0000-000000000001"), "WalletCredit", 100m, "WalletCredit",
                    100m, 299m, 30,
                    null, true, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral");

            migrationBuilder.DropTable(
                name: "referral_program_config");

            migrationBuilder.DropIndex(
                name: "ix_customer_referral_code",
                table: "customer");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "customer");
        }
    }
}
