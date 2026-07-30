using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_mobile_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_address_id = table.Column<Guid>(type: "uuid", nullable: true),
                    address_label_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_line1snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    address_line2snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    address_landmark_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_pincode_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_city_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_state_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_latitude_snapshot = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    address_longitude_snapshot = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    address_contact_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_contact_mobile_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slot_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_window_name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slot_start_time_snapshot = table.Column<TimeSpan>(type: "interval", nullable: false),
                    slot_end_time_snapshot = table.Column<TimeSpan>(type: "interval", nullable: false),
                    base_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity_snapshot = table.Column<int>(type: "integer", nullable: false),
                    base_total_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    add_on_total_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    visit_charge_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    subtotal_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tax_percentage_snapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tax_amount_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    platform_fee_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_payable_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    coupon_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    coupon_discount_amount_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_item_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_status_history_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_addon_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_add_on_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_addon_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_addon_item_booking_items_booking_item_id",
                        column: x => x.booking_item_id,
                        principalTable: "booking_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_created_at_utc",
                table: "booking",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_booking_customer_id_status",
                table: "booking",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_addon_item_booking_item_id",
                table: "booking_addon_item",
                column: "booking_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_item_booking_id",
                table: "booking_item",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_status_history_booking_id_changed_at_utc",
                table: "booking_status_history",
                columns: new[] { "booking_id", "changed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_addon_item");

            migrationBuilder.DropTable(
                name: "booking_status_history");

            migrationBuilder.DropTable(
                name: "booking_item");

            migrationBuilder.DropTable(
                name: "booking");
        }
    }
}
