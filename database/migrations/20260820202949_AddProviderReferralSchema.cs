using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderReferralSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "referral_code",
                table: "provider",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "provider_referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referee_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_code_used = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    qualifying_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referrer_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referee_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    qualifying_completed_jobs_count = table.Column<int>(type: "integer", nullable: false),
                    referrer_earning_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referee_earning_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    qualified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rewarded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_fraud_flagged = table.Column<bool>(type: "boolean", nullable: false),
                    fraud_review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fraud_reviewed_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fraud_reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_referral", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provider_referral_program_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    referee_reward_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    qualifying_completed_jobs_count = table.Column<int>(type: "integer", nullable: false),
                    referral_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    max_referrals_per_provider = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_referral_program_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_code",
                table: "provider",
                column: "referral_code",
                unique: true,
                filter: "referral_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_is_fraud_flagged",
                table: "provider_referral",
                column: "is_fraud_flagged");

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_referee_provider_id",
                table: "provider_referral",
                column: "referee_provider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_referrer_provider_id",
                table: "provider_referral",
                column: "referrer_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_status",
                table: "provider_referral",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_provider_referral_status_expires_at_utc",
                table: "provider_referral",
                columns: new[] { "status", "expires_at_utc" });

            // Seed one default config row, same convention as
            // 20260801150457_AddReferralSchema.cs - GetAsync returns a
            // "not initialized" error rather than a lazy default when the row
            // is missing, so this table needs at least one row for the
            // provider referral flow to ever start. Defaults are conservative
            // placeholders admins are expected to tune via the config API
            // before launching the program; both reward values are higher
            // than the customer program's default (Rs 100/side) because a
            // provider referral pays for a new *worker*, not a single order.
            migrationBuilder.InsertData(
                table: "provider_referral_program_config",
                columns: new[]
                {
                    "id", "referrer_reward_value", "referee_reward_value",
                    "qualifying_completed_jobs_count", "referral_expiry_days",
                    "max_referrals_per_provider", "is_active", "updated_at_utc", "updated_by_admin_user_id"
                },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0000-000000000001"), 500m, 500m,
                    3, 45,
                    null, true, new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc), null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_referral");

            migrationBuilder.DropTable(
                name: "provider_referral_program_config");

            migrationBuilder.DropIndex(
                name: "ix_provider_referral_code",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "provider");
        }
    }
}
