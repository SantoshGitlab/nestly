using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_flagged",
                table: "review",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "moderated_at_utc",
                table: "review",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "moderated_by_admin_user_id",
                table: "review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderator_note",
                table: "review",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_status_is_flagged",
                table: "review",
                columns: new[] { "status", "is_flagged" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_review_status_is_flagged",
                table: "review");

            migrationBuilder.DropColumn(
                name: "is_flagged",
                table: "review");

            migrationBuilder.DropColumn(
                name: "moderated_at_utc",
                table: "review");

            migrationBuilder.DropColumn(
                name: "moderated_by_admin_user_id",
                table: "review");

            migrationBuilder.DropColumn(
                name: "moderator_note",
                table: "review");
        }
    }
}
