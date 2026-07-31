using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Reviews;

/// <summary>Admin review moderation (SRS 12.15, task 122): filtered search, hide/unhide, flag/unflag, and CSV export.</summary>
public interface IReviewModerationService
{
    /// <summary>Filtered, paginated review search for the moderation screen.</summary>
    Task<Result<ReviewModerationSearchResponse>> SearchAsync(ReviewModerationSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Hides a review from the public/customer-facing side. The original rating/text/tags are never mutated - only visibility state changes.</summary>
    Task<Result<ReviewModerationResponse>> HideAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Restores a hidden review to public visibility.</summary>
    Task<Result<ReviewModerationResponse>> UnhideAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Flags a review as abusive/inappropriate content, independent of its visibility.</summary>
    Task<Result<ReviewModerationResponse>> FlagAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Clears a review's abuse flag.</summary>
    Task<Result<ReviewModerationResponse>> UnflagAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exports every review matching the given filter as CSV bytes (SRS 12.15 "Export reviews").</summary>
    Task<Result<byte[]>> ExportCsvAsync(ReviewModerationSearchRequest request, CancellationToken cancellationToken = default);
}
