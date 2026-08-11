using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceGroupIdToBookingItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "service_group_id",
                table: "booking_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_group_name_snapshot",
                table: "booking_item",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "service_group_id",
                table: "booking_item");

            migrationBuilder.DropColumn(
                name: "service_group_name_snapshot",
                table: "booking_item");
        }
    }
}
