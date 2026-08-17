namespace Nestly.Application.CustomerRatings;

/// <summary>Whether a completed job can be rated right now - the reverse-direction analogue of <c>ReviewEligibilityResponse</c>.</summary>
public record CustomerRatingEligibilityResponse(bool IsEligible, string? IneligibilityReason);

/// <summary>A provider's private rating of the customer they just worked for (task: bidirectional reviews).</summary>
public record SubmitCustomerRatingRequest(int Rating, string? Note);

/// <summary>A submitted customer rating.</summary>
public record CustomerRatingResponse(
    Guid Id,
    Guid BookingId,
    Guid CustomerId,
    int Rating,
    string? Note,
    DateTime CreatedAtUtc);

/// <summary>
/// A customer's rating rolled up from every rating providers have left about
/// them - the admin-only, reverse-direction analogue of <c>ProviderRatingSummary</c>.
/// Absent entirely when the customer has no ratings yet, same "no rating" vs
/// "rated 0" distinction that type preserves.
/// </summary>
/// <param name="AverageRating">Rounded to one decimal, same precision convention as <c>ProviderRatingSummary.AverageRating</c>.</param>
/// <param name="RatingCount">The honest denominator the average is over.</param>
public sealed record CustomerRatingSummary(Guid CustomerId, double AverageRating, int RatingCount);

/// <summary>One rating row for the Customer 360 view (admin-api), joined with the rating provider's display name.</summary>
public sealed record CustomerRatingRow(
    Guid Id,
    Guid BookingId,
    int Rating,
    string? Note,
    string ProviderDisplayName,
    DateTime CreatedAtUtc);
