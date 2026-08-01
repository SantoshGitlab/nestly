using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralMilestonesAndWalletCreditExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at_utc",
                table: "wallet_ledger",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "remaining_amount",
                table: "wallet_ledger",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "referral_milestone",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    threshold_count = table.Column<int>(type: "integer", nullable: false),
                    bonus_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bonus_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_milestone", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_milestone_award",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    awarded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_milestone_award", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ledger_expires_at_utc_remaining_amount",
                table: "wallet_ledger",
                columns: new[] { "expires_at_utc", "remaining_amount" });

            migrationBuilder.CreateIndex(
                name: "ix_referral_milestone_threshold_count",
                table: "referral_milestone",
                column: "threshold_count",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_milestone_award_referral_milestone_id_referrer_cus",
                table: "referral_milestone_award",
                columns: new[] { "referral_milestone_id", "referrer_customer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_milestone");

            migrationBuilder.DropTable(
                name: "referral_milestone_award");

            migrationBuilder.DropIndex(
                name: "ix_wallet_ledger_expires_at_utc_remaining_amount",
                table: "wallet_ledger");

            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                table: "wallet_ledger");

            migrationBuilder.DropColumn(
                name: "remaining_amount",
                table: "wallet_ledger");
        }
    }
}
