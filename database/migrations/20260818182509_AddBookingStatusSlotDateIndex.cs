using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 333: the index behind <c>BookingFulfilmentPromotionJob</c>'s
    /// candidate query - "Confirmed bookings whose slot starts inside the lead
    /// window" - which runs every five minutes on the admin API's Hangfire
    /// server.
    ///
    /// <c>booking</c> already had <c>(customer_id, status)</c> and
    /// <c>(created_at_utc)</c>; neither can lead a query keyed on status alone,
    /// so without this the sweep degrades to a sequential scan over a table
    /// that only ever grows and is, in steady state, almost entirely terminal
    /// rows the query can never match.
    ///
    /// Column order is status then slot_date: status is the selective side
    /// (Confirmed is a narrow slice of a mature booking table), and slot_date
    /// second lets the same index serve both the range predicate and the
    /// order-by the job pages on.
    /// </summary>
    public partial class AddBookingStatusSlotDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_booking_status_slot_date",
                table: "booking",
                columns: new[] { "status", "slot_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_booking_status_slot_date",
                table: "booking");
        }
    }
}
