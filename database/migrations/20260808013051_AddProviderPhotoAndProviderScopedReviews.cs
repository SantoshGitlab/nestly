using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nestly.Infrastructure.Migrations
{
    /// <summary>
    /// Task 293: gives the two fields <c>BookingProviderSummary</c> promised
    /// but could not back real data.
    ///
    /// <list type="number">
    /// <item><c>provider.photo_url</c> plus its moderation quartet. A photo is
    /// a reference to an already-hosted image, same convention as
    /// <c>provider_kyc_document.file_ref</c> - this schema still has no blob
    /// storage and this migration does not invent one. The moderation columns
    /// are what make the photo customer-visible: only an Approved one is ever
    /// served (<c>Provider.PublicPhotoUrl</c>).</item>
    /// <item><c>review.provider_id</c> - a nullable FK to <c>provider</c>,
    /// Restrict - which turns reviews from service-scoped to provider-scoped
    /// and makes "this professional is rated 4.8" computable at all.</item>
    /// </list>
    ///
    /// <b>The backfill, and why it deliberately leaves rows null.</b>
    /// A review's provider is not simply <c>booking.assigned_provider_id</c>.
    /// That column holds whoever is on the booking NOW, and a booking can be
    /// reassigned - so on a reassigned booking it may name someone who never
    /// did the job. Attributing a one-star review to the wrong professional is
    /// a far worse outcome than showing them no rating, so the backfill only
    /// attributes a review when the booking's assignment history names exactly
    /// one provider. Any booking that ever involved a second provider leaves
    /// its review's <c>provider_id</c> NULL - unattributed, counting towards
    /// nobody's rating - rather than guessing. Same for a booking with no
    /// provider recorded at all.
    ///
    /// That is also why the column is nullable and must stay nullable: those
    /// two historic populations exist, so NOT NULL is not expressible here,
    /// and going forward a review whose provider cannot be resolved is a real
    /// state rather than an error. <c>Review.ProviderId</c> documents the
    /// same contract on the entity.
    ///
    /// The backfill is one statement and it is idempotent: it only ever writes
    /// rows whose <c>provider_id</c> is still NULL, so re-running it (a failed
    /// deploy, a replay) cannot reattribute a review that has since been
    /// corrected by hand.
    ///
    /// <c>Down</c> is the exact inverse of <c>Up</c>: dropping
    /// <c>review.provider_id</c> discards the backfilled attributions along
    /// with the column, which is correct - they are derived data with no
    /// separate source of truth to preserve.
    /// </summary>
    public partial class AddProviderPhotoAndProviderScopedReviews : Migration
    {
        /// <summary>
        /// The backfill, as a constant so the rule it encodes is testable
        /// rather than only reviewable.
        ///
        /// A migration is normally self-contained and frozen, and this stays
        /// both - the statement lives here, depends on nothing outside this
        /// file, and must never be edited once deployed. Exposing it is what
        /// lets <c>ProviderScopedReviewBackfillTests</c> execute this exact
        /// string against a seeded database and assert on which rows it does
        /// and does not attribute. The alternative was the situation
        /// <c>AddProviderNoDoubleBooking</c> had to accept and write down: DDL
        /// no test can reach, verified only by hand. A rule about who gets
        /// blamed for a one-star review deserves better than that.
        ///
        /// The <c>NOT EXISTS</c> clause is the whole safety rule. It leaves a
        /// review unattributed the moment the booking's assignment history
        /// shows any provider other than the one currently on it - which
        /// covers every reassignment shape (a rejected offer, an admin swap
        /// mid-job, a withdrawal followed by a new assignment) without having
        /// to interpret assignment statuses, because none of those shapes lets
        /// us prove who was standing in the customer's home when the review
        /// was written.
        ///
        /// The <c>provider_id IS NULL</c> guard makes it idempotent: a replay
        /// or a retried deploy cannot reattribute a review that has since been
        /// corrected by hand.
        /// </summary>
        public const string BackfillSql = """
            UPDATE review AS r
            SET provider_id = b.assigned_provider_id
            FROM booking AS b
            WHERE r.booking_id = b.id
              AND r.provider_id IS NULL
              AND b.assigned_provider_id IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM booking_provider_assignment AS a
                  WHERE a.booking_id = b.id
                    AND a.provider_id <> b.assigned_provider_id
              );
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "provider_id",
                table: "review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "photo_moderated_at_utc",
                table: "provider",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "photo_moderated_by_admin_user_id",
                table: "provider",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_moderation_note",
                table: "provider",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_moderation_status",
                table: "provider",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "provider",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_provider_id_status",
                table: "review",
                columns: new[] { "provider_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_photo_moderation_status",
                table: "provider",
                column: "photo_moderation_status");

            migrationBuilder.AddForeignKey(
                name: "fk_review_provider_provider_id",
                table: "review",
                column: "provider_id",
                principalTable: "provider",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Last statement in Up, and deliberately after the foreign key:
            // the constraint is already in force over every row this writes,
            // so a bad attribution cannot be persisted at all. See
            // BackfillSql for the rule itself.
            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_provider_provider_id",
                table: "review");

            migrationBuilder.DropIndex(
                name: "ix_review_provider_id_status",
                table: "review");

            migrationBuilder.DropIndex(
                name: "ix_provider_photo_moderation_status",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "provider_id",
                table: "review");

            migrationBuilder.DropColumn(
                name: "photo_moderated_at_utc",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "photo_moderated_by_admin_user_id",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "photo_moderation_note",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "photo_moderation_status",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "provider");
        }
    }
}
