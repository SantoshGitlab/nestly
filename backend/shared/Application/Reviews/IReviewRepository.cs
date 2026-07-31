using Nestly.Domain;

namespace Nestly.Application.Reviews;

public interface IReviewRepository
{
    Task AddAsync(Review review);

    Task<Review?> GetByBookingIdAsync(Guid bookingId);

    Task<IReadOnlyList<Review>> ListByServiceAsync(Guid serviceId);

    /// <summary>The bare review entity for a moderation action (task 122) - no joined display fields, just the aggregate to mutate.</summary>
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists a moderation action (hide/unhide/flag/unflag/note) made through <see cref="Review"/>'s own mutators.</summary>
    Task UpdateAsync(Review review, CancellationToken cancellationToken = default);

    /// <summary>One review joined with the customer/service/category names the moderation screen displays (task 122), or null if it does not exist.</summary>
    Task<ReviewModerationRow?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Filtered, paginated admin review search (SRS 12.15, task 122).</summary>
    Task<ReviewModerationSearchResult> SearchAsync(ReviewModerationCriteria criteria, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The full (unpaginated, capped) set of rows matching an export request (SRS 12.15 "Export reviews", task 122).</summary>
    Task<IReadOnlyList<ReviewModerationRow>> ListForExportAsync(ReviewModerationCriteria criteria, CancellationToken cancellationToken = default);
}
