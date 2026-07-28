using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCommunicationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_communication_preference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transactional_sms_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    transactional_email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    transactional_whatsapp_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    promotional_sms_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    promotional_email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    promotional_whatsapp_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    push_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_communication_preference", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_communication_preference_customer_id",
                table: "customer_communication_preference",
                column: "customer_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_communication_preference");
        }
    }
}
