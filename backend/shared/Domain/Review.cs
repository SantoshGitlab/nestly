using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's review of a completed booking (SRS 11.16, 17, tasks 84a,
/// 85a-c). At most one primary review per booking (SRS 11.16.3 "one booking
/// should have one primary review record") - enforced here via a unique
/// index on <see cref="BookingId"/> (see <c>ReviewConfiguration</c>), not
/// only in the service layer.
/// </summary>
public class Review : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ServiceId { get; private set; }

    /// <summary>
    /// Task 293: WHO the review is about, not just what was bought. A review
    /// used to name only its service, which made "this professional is rated
    /// 4.8" uncomputable - the product decision recorded there is that
    /// reviews are provider-scoped from now on.
    ///
    /// Nullable, and it has to stay nullable. Two populations resolve to no
    /// provider and neither is an error: historic reviews backfilled from a
    /// booking that was reassigned between providers (attributing those to
    /// whoever happens to hold <c>booking.assigned_provider_id</c> today
    /// would put a bad review on a professional who did not do the job - see
    /// the backfill migration), and any future review on a booking that
    /// somehow completed without a provider on it. A null here means "not
    /// attributable", and such a review counts towards no one's rating.
    /// </summary>
    public Guid? ProviderId { get; private set; }

    public int Rating { get; private set; }
    public string? ReviewText { get; private set; }

    /// <summary>Optional issue tags (SRS 11.16.2), stored as a simple comma-separated list - no separate tag taxonomy exists yet.</summary>
    public string? IssueTags { get; private set; }

    public ReviewStatus Status { get; private set; }

    /// <summary>Marked for abusive/inappropriate content (SRS 12.15 "Flag abusive content", task 122) - independent of <see cref="Status"/>, so a flagged review can still be visible while awaiting a moderator's decision.</summary>
    public bool IsFlagged { get; private set; }

    /// <summary>The moderator's most recent reason/context for the last hide/unhide/flag/unflag action (task 122). The full history of every action lives in the audit trail (<c>IAuditLogWriter</c>); this is only the current note shown on the moderation screen.</summary>
    public string? ModeratorNote { get; private set; }

    /// <summary>Admin user who performed the most recent moderation action, if any - traceability only, not a foreign key any other aggregate depends on (same rationale as <see cref="CustomerNote.AuthorAdminUserId"/>).</summary>
    public Guid? ModeratedByAdminUserId { get; private set; }

    public DateTime? ModeratedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    protected Review() { }

    public Review(Guid id, Guid bookingId, Guid customerId, Guid serviceId, Guid? providerId, int rating, string? reviewText, string? issueTags = null)
        : base(id)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        BookingId = bookingId;
        CustomerId = customerId;
        ServiceId = serviceId;
        ProviderId = providerId;
        Rating = rating;
        ReviewText = reviewText;
        IssueTags = issueTags;
        Status = ReviewStatus.Visible;
        IsFlagged = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin moderation (SRS 12.15, 17.2, task 122). These are purely additive
    /// state changes - the original <see cref="Rating"/>, <see cref="ReviewText"/>
    /// and <see cref="IssueTags"/> the customer submitted are never mutated,
    /// so the source review always remains available for audit even after it
    /// is hidden, flagged, or annotated.
    /// </summary>
    public void Hide(Guid moderatorAdminUserId, string? note) => ApplyModeration(moderatorAdminUserId, note, status: ReviewStatus.Hidden);

    public void MakeVisible(Guid moderatorAdminUserId, string? note) => ApplyModeration(moderatorAdminUserId, note, status: ReviewStatus.Visible);

    public void Flag(Guid moderatorAdminUserId, string? note) => ApplyModeration(moderatorAdminUserId, note, isFlagged: true);

    public void Unflag(Guid moderatorAdminUserId, string? note) => ApplyModeration(moderatorAdminUserId, note, isFlagged: false);

    private void ApplyModeration(Guid moderatorAdminUserId, string? note, ReviewStatus? status = null, bool? isFlagged = null)
    {
        if (status.HasValue)
        {
            Status = status.Value;
        }

        if (isFlagged.HasValue)
        {
            IsFlagged = isFlagged.Value;
        }

        if (note is not null)
        {
            ModeratorNote = note;
        }

        ModeratedByAdminUserId = moderatorAdminUserId;
        ModeratedAtUtc = DateTime.UtcNow;
    }
}
