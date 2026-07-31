using Nestly.Domain;

namespace Nestly.Application.Reviews;

/// <summary>Review eligibility for a booking (SRS 11.16.1, 11.16.3, tasks 85a, 85c).</summary>
public record ReviewEligibilityResponse(bool IsEligible, string? IneligibilityReason);

/// <summary>Customer-submitted review (SRS 11.16.2).</summary>
public record SubmitReviewRequest(int Rating, string? ReviewText, string? IssueTags);

/// <summary>A submitted review (SRS 17.1).</summary>
public record ReviewResponse(
    Guid Id,
    Guid BookingId,
    Guid ServiceId,
    int Rating,
    string? ReviewText,
    string? IssueTags,
    ReviewStatus Status,
    DateTime CreatedAtUtc);
