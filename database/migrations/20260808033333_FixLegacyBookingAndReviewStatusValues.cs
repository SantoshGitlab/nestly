using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only fix for pre-rename status strings still sitting in
    /// <c>booking.status</c> and <c>review.status</c> that <see
    /// cref="Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal.StringEnumConverter{TModel,TProvider,TStore}"/>
    /// cannot convert, throwing InvalidOperationException whenever a query's
    /// result set includes one of these rows (admin bookings search, the
    /// booking-revenue report, and review moderation search/eligibility all
    /// hit this - any query over <c>Booking</c>/<c>Review</c> that touches an
    /// affected row 500s; single-row/narrow-range queries only "worked" by
    /// accident of not touching one).
    ///
    /// - booking.status = 'Requested' -&gt; 'Initiated': <see cref="Nestly.Domain.BookingStatus"/>
    ///   has never had a "Requested" member (git history), and
    ///   booking_status_history has 70 "Initiated" to_status rows against 21
    ///   "Requested" rows on booking itself - Initiated is booking's actual
    ///   first lifecycle state (BookingStatusMapper: "Booking Started").
    /// - booking.status = 'Cancelled' -&gt; 'CancelledByCustomer': the enum
    ///   only has CancelledByCustomer/CancelledByAdmin, never a bare
    ///   "Cancelled". booking_status_history's to_status is
    ///   'CancelledByCustomer' for every one of its cancellation-shaped
    ///   transitions (17 rows) and never CancelledByAdmin, so that's the
    ///   value being approximated.
    /// - review.status = 'Published' -&gt; 'Visible': <see cref="Nestly.Domain.ReviewStatus"/>
    ///   only has Visible/Hidden (git history: never renamed, never had a
    ///   third value) and every review-creation code path
    ///   (<c>Review</c> ctor, <c>ReviewRepository</c>) only ever writes
    ///   Visible/Hidden - "Published" was never emitted by the app, so this
    ///   is stale/malformed data, not a genuinely distinct lifecycle state.
    /// </summary>
    public partial class FixLegacyBookingAndReviewStatusValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE booking SET status = 'Initiated' WHERE status = 'Requested';");
            migrationBuilder.Sql("UPDATE booking SET status = 'CancelledByCustomer' WHERE status = 'Cancelled';");
            migrationBuilder.Sql("UPDATE review SET status = 'Visible' WHERE status = 'Published';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: the values being replaced were never
            // valid application data (not members of BookingStatus/ReviewStatus,
            // never written by any code path), so there is nothing correct to
            // roll back to.
        }
    }
}
