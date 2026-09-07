using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderKycNotificationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "notification_event",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "provider_id",
                table: "notification_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_event_provider_id",
                table: "notification_event",
                column: "provider_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notification_event_providers_provider_id",
                table: "notification_event",
                column: "provider_id",
                principalTable: "provider",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_event_providers_provider_id",
                table: "notification_event");

            migrationBuilder.DropIndex(
                name: "ix_notification_event_provider_id",
                table: "notification_event");

            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "notification_event");

            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "notification_event",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
