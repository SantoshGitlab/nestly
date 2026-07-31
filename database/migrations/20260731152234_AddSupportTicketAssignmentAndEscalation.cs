using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTicketAssignmentAndEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_admin_user_id",
                table: "support_ticket",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at_utc",
                table: "support_ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "escalated_at_utc",
                table: "support_ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_assigned_admin_user_id",
                table: "support_ticket",
                column: "assigned_admin_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_support_ticket_admin_user_assigned_admin_user_id",
                table: "support_ticket",
                column: "assigned_admin_user_id",
                principalTable: "admin_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_support_ticket_admin_user_assigned_admin_user_id",
                table: "support_ticket");

            migrationBuilder.DropIndex(
                name: "ix_support_ticket_assigned_admin_user_id",
                table: "support_ticket");

            migrationBuilder.DropColumn(
                name: "assigned_admin_user_id",
                table: "support_ticket");

            migrationBuilder.DropColumn(
                name: "assigned_at_utc",
                table: "support_ticket");

            migrationBuilder.DropColumn(
                name: "escalated_at_utc",
                table: "support_ticket");
        }
    }
}
