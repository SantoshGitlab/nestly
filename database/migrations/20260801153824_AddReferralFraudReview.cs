using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralFraudReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fraud_review_note",
                table: "referral",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fraud_reviewed_at_utc",
                table: "referral",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fraud_reviewed_by_admin_user_id",
                table: "referral",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_fraud_flagged",
                table: "referral",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_referral_is_fraud_flagged",
                table: "referral",
                column: "is_fraud_flagged");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_referral_is_fraud_flagged",
                table: "referral");

            migrationBuilder.DropColumn(
                name: "fraud_review_note",
                table: "referral");

            migrationBuilder.DropColumn(
                name: "fraud_reviewed_at_utc",
                table: "referral");

            migrationBuilder.DropColumn(
                name: "fraud_reviewed_by_admin_user_id",
                table: "referral");

            migrationBuilder.DropColumn(
                name: "is_fraud_flagged",
                table: "referral");
        }
    }
}
