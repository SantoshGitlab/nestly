using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "booking_reference",
                table: "booking",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill: every row that existed before this migration got the
            // column's "" default above, which would collide against itself
            // the moment there is more than one such row - the unique index
            // below cannot go on until every row has a real value. Booking.cs's
            // own generator never runs for historical rows (it only fires in
            // the constructor, at insert time), so this is the one place that
            // logic is duplicated, in SQL, for existing data only - new rows
            // from here on get their reference from the domain constructor as
            // normal. md5(id) rather than the app's random-bytes alphabet
            // because this runs inside the database, not the app process; the
            // id is already globally unique, so this is unique too.
            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking_reference =
                    'NST-' || to_char(created_at_utc, 'YYMMDD') || '-' ||
                    upper(substr(md5(id::text), 1, 5))
                WHERE booking_reference = '';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_booking_booking_reference",
                table: "booking",
                column: "booking_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_booking_booking_reference",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "booking_reference",
                table: "booking");
        }
    }
}
