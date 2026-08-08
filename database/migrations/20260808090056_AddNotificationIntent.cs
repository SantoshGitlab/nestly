using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_intent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    domain_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_intent", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_dedupe_key",
                table: "notification_intent",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_domain_event_id",
                table: "notification_intent",
                column: "domain_event_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_status_created_at_utc",
                table: "notification_intent",
                columns: new[] { "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_intent");
        }
    }
}
