using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A provider's private rating of the customer on a completed booking - the
/// reverse direction of <see cref="Review"/>. Deliberately a separate entity
/// rather than a second row shape on <c>Review</c>: <c>Review</c>'s unique
/// index on <see cref="Review.BookingId"/> means a customer review and a
/// provider rating of the same booking cannot coexist as rows in the same
/// table, and the two also differ in who may ever see them - a customer
/// review is public/moderated content, this is an internal-only signal
/// (admin risk visibility on the Customer 360 view) that the rated customer
/// never sees. Not moderated for the same reason: there is no public surface
/// for a moderator to hide this from.
///
/// At most one rating per booking - enforced via a unique index on
/// <see cref="BookingId"/> (see <c>CustomerRatingConfiguration</c>), mirroring
/// <see cref="Review"/>'s "one primary review per booking" invariant.
/// </summary>
public class CustomerRating : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int Rating { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected CustomerRating() { }

    public CustomerRating(Guid id, Guid bookingId, Guid providerId, Guid customerId, int rating, string? note)
        : base(id)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        BookingId = bookingId;
        ProviderId = providerId;
        CustomerId = customerId;
        Rating = rating;
        Note = note;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
