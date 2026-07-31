namespace Nestly.Domain;

/// <summary>
/// Review visibility, moderated by admin (SRS 11.16.3, 12.15, 17.2). Only two
/// states: whether the review is shown on the public/customer-facing side.
/// "Flagged for abuse" (SRS 12.15 "Flag abusive content") is a separate,
/// orthogonal concern - see <see cref="Review.IsFlagged"/> - since a review
/// under review for abuse should still be able to stay visible (or hidden)
/// independently of that flag.
/// </summary>
public enum ReviewStatus
{
    Visible,
    Hidden
}
