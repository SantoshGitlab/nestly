using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 288: a database-level backstop for "one provider cannot be in two
    /// places at once." The authoritative rule is
    /// <c>IProviderScheduleConflictService</c>, applied by
    /// <c>BookingProviderAssignmentService</c> on both the manual admin and
    /// the automatic assignment path; this constraint exists so that a lost
    /// race - or any future writer that bypasses that service - still cannot
    /// leave two overlapping live jobs on one provider.
    ///
    /// Three things worth knowing before changing this:
    ///
    /// 1. It has to live on <c>booking</c>, not on
    ///    <c>booking_provider_assignment</c>. An exclusion constraint can only
    ///    reference columns of a single table, and <c>booking</c> is the only
    ///    place where the provider (<c>assigned_provider_id</c>) and the slot
    ///    (<c>slot_date</c> + the two time snapshots) sit side by side. That
    ///    means the predicate below approximates "live assignment" through
    ///    <c>booking.status</c> rather than through
    ///    <c>booking_provider_assignment.status</c>, which is what the
    ///    application check actually reads. The approximation is deliberately
    ///    narrower than the application rule, never wider: every status listed
    ///    is one the application also treats as a conflict, so the constraint
    ///    can reject only writes the service would have rejected anyway. A
    ///    Rejected assignment already clears <c>assigned_provider_id</c>
    ///    (<c>RejectInternalAsync</c>) and a Withdrawn one leaves the booking
    ///    Cancelled, so neither is covered here.
    ///
    /// 2. Half-open <c>'[)'</c> ranges, matching the application predicate
    ///    (<c>NewStart &lt; ExistingEnd &amp;&amp; ExistingStart &lt; NewEnd</c>):
    ///    back-to-back 09:00-11:00 and 11:00-13:00 jobs touch at an endpoint
    ///    and must both stay legal.
    ///
    /// 3. It is PostgreSQL-only and it is NOT exercised by the test suite.
    ///    Catalog.Tests builds its schema with <c>EnsureCreated</c> over
    ///    in-memory SQLite, which never runs migrations - and SQLite has no
    ///    exclusion constraints at all, so there is nothing to translate this
    ///    into. The tests therefore prove the service-level rule only; a green
    ///    suite is not evidence that this DDL is correct. Same class of
    ///    runtime/test-provider divergence task 252 recorded for SQLite
    ///    tolerating a negative OFFSET - written down rather than hidden.
    ///    Because CI cannot cover it, this statement was instead verified by
    ///    hand against PostgreSQL 16 (the docker-compose image): applied to
    ///    the dev <c>booking</c> table inside a rolled-back transaction, where
    ///    it was accepted and no existing row violated it, and its semantics
    ///    checked on a scratch table - overlapping rejected, back-to-back
    ///    11:00 after 09:00-11:00 accepted, same slot for a different provider
    ///    or a different date accepted, overlapping-but-cancelled accepted.
    ///    Re-verify the same way if the predicate is ever edited.
    /// </summary>
    public partial class AddProviderNoDoubleBooking : Migration
    {
        private const string ConstraintName = "ex_booking_provider_no_double_booking";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Needed for the `assigned_provider_id WITH =` operator: gist has
            // no equality opclass for uuid without it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // slot_date is `date` and the two snapshots are `interval`, so
            // `slot_date + slot_start_time_snapshot` is an immutable
            // date-plus-interval yielding `timestamp` - which is what an
            // exclusion constraint's expression has to be.
            //
            // The `slot_end_time_snapshot > slot_start_time_snapshot` guard is
            // not part of the business rule: tsrange() raises on a lower bound
            // above its upper bound, and a degenerate or overnight slot
            // snapshot should be a data-quality problem, not an INSERT that
            // explodes on an unrelated booking.
            migrationBuilder.Sql($"""
                ALTER TABLE booking
                    ADD CONSTRAINT {ConstraintName}
                    EXCLUDE USING gist (
                        assigned_provider_id WITH =,
                        tsrange(
                            slot_date + slot_start_time_snapshot,
                            slot_date + slot_end_time_snapshot,
                            '[)'
                        ) WITH &&
                    )
                    WHERE (
                        assigned_provider_id IS NOT NULL
                        AND slot_end_time_snapshot > slot_start_time_snapshot
                        AND status IN ('Assigned', 'ProviderEnRoute', 'ProviderArrived', 'InProgress', 'Completed')
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"ALTER TABLE booking DROP CONSTRAINT IF EXISTS {ConstraintName};");

            // btree_gist is deliberately left installed: dropping an extension
            // is not this migration's to reverse once anything else in the
            // database may have come to depend on it.
        }
    }
}
