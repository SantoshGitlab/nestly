using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTokenProviderOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "device_token",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "provider_id",
                table: "device_token",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_token_provider_id_is_active",
                table: "device_token",
                columns: new[] { "provider_id", "is_active" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_device_token_exactly_one_owner",
                table: "device_token",
                sql: "(\"customer_id\" IS NOT NULL AND \"provider_id\" IS NULL) OR (\"customer_id\" IS NULL AND \"provider_id\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_device_token_providers_provider_id",
                table: "device_token",
                column: "provider_id",
                principalTable: "provider",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_token_providers_provider_id",
                table: "device_token");

            migrationBuilder.DropIndex(
                name: "ix_device_token_provider_id_is_active",
                table: "device_token");

            migrationBuilder.DropCheckConstraint(
                name: "ck_device_token_exactly_one_owner",
                table: "device_token");

            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "device_token");

            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "device_token",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
