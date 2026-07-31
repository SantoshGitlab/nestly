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
    public int Rating { get; private set; }
    public string? ReviewText { get; private set; }

    /// <summary>Optional issue tags (SRS 11.16.2), stored as a simple comma-separated list - no separate tag taxonomy exists yet.</summary>
    public string? IssueTags { get; private set; }

    public ReviewStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected Review() { }

    public Review(Guid id, Guid bookingId, Guid customerId, Guid serviceId, int rating, string? reviewText, string? issueTags = null)
        : base(id)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        BookingId = bookingId;
        CustomerId = customerId;
        ServiceId = serviceId;
        Rating = rating;
        ReviewText = reviewText;
        IssueTags = issueTags;
        Status = ReviewStatus.Visible;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Admin moderation (SRS 12.15, 17.2) - the original record is retained, only its visibility changes.</summary>
    public void Hide() => Status = ReviewStatus.Hidden;

    public void Flag() => Status = ReviewStatus.Flagged;

    public void MakeVisible() => Status = ReviewStatus.Visible;
}
