using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Csv;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin review moderation (SRS 12.15, task 122): filtered search, hide/
/// unhide, flag/unflag, and CSV export. Every moderation action is written to
/// the existing audit trail via <see cref="IAuditLogWriter"/> (task 20/95g's
/// mechanism) rather than a second, review-specific history table - the
/// review row itself only ever gains state (<see cref="Review.Hide"/> and
/// friends never touch <see cref="Review.Rating"/>/<see cref="Review.ReviewText"/>/
/// <see cref="Review.IssueTags"/>), so the original submission is always
/// recoverable both from the review row and from the audit log's old/new
/// value snapshots.
/// </summary>
public class ReviewModerationService : IReviewModerationService
{
    // String enum converter so the audit trail's old/new value snapshots read
    // as "Hidden"/"Visible" rather than opaque integers - matching how
    // ReviewConfiguration already stores Status as a string in the database.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IReviewRepository _reviewRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public ReviewModerationService(IReviewRepository reviewRepository, IAuditLogWriter auditLogWriter)
    {
        _reviewRepository = reviewRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<Result<ReviewModerationSearchResponse>> SearchAsync(ReviewModerationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _reviewRepository.SearchAsync(request.ToCriteria(), request.Page, request.PageSize, cancellationToken);

        var items = result.Rows.Select(ToResponse).ToList();
        return new ReviewModerationSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public Task<Result<ReviewModerationResponse>> HideAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default) =>
        ModerateAsync(reviewId, moderatorAdminUserId, request, "Hidden", review => review.Hide(moderatorAdminUserId, request.Note), cancellationToken);

    public Task<Result<ReviewModerationResponse>> UnhideAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default) =>
        ModerateAsync(reviewId, moderatorAdminUserId, request, "Unhidden", review => review.MakeVisible(moderatorAdminUserId, request.Note), cancellationToken);

    public Task<Result<ReviewModerationResponse>> FlagAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default) =>
        ModerateAsync(reviewId, moderatorAdminUserId, request, "Flagged", review => review.Flag(moderatorAdminUserId, request.Note), cancellationToken);

    public Task<Result<ReviewModerationResponse>> UnflagAsync(Guid reviewId, Guid moderatorAdminUserId, ModerateReviewRequest request, CancellationToken cancellationToken = default) =>
        ModerateAsync(reviewId, moderatorAdminUserId, request, "Unflagged", review => review.Unflag(moderatorAdminUserId, request.Note), cancellationToken);

    public async Task<Result<byte[]>> ExportCsvAsync(ReviewModerationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await _reviewRepository.ListForExportAsync(request.ToCriteria(), cancellationToken);
        return Result.Success(BuildCsv(rows));
    }

    private async Task<Result<ReviewModerationResponse>> ModerateAsync(
        Guid reviewId,
        Guid moderatorAdminUserId,
        ModerateReviewRequest request,
        string action,
        Action<Review> applyModeration,
        CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
        {
            return Error.NotFound("Review.NotFound", "The specified review does not exist.");
        }

        string oldValuesJson = SerializeModerationState(review);
        applyModeration(review);
        string newValuesJson = SerializeModerationState(review);

        await _auditLogWriter.WriteAsync(
            new AuditEntry("Review", review.Id.ToString(), action, oldValuesJson, newValuesJson),
            cancellationToken);

        // Same DbContext as the audit writer above (both scoped per request) -
        // this single SaveChangesAsync commits the moderation state change
        // and its audit row together (IAuditLogWriter.WriteAsync's documented
        // contract).
        await _reviewRepository.UpdateAsync(review, cancellationToken);

        var row = await _reviewRepository.GetRowByIdAsync(reviewId, cancellationToken);
        if (row is null)
        {
            return Error.NotFound("Review.NotFound", "The specified review does not exist.");
        }

        return ToResponse(row);
    }

    private static string SerializeModerationState(Review review) => JsonSerializer.Serialize(
        new
        {
            review.Status,
            review.IsFlagged,
            review.ModeratorNote
        },
        JsonOptions);

    private static ReviewModerationResponse ToResponse(ReviewModerationRow row) => new(
        row.Review.Id,
        row.Review.BookingId,
        row.Review.CustomerId,
        row.CustomerName,
        row.Review.ServiceId,
        row.ServiceName,
        row.CategoryId,
        row.CategoryName,
        row.Review.Rating,
        row.Review.ReviewText,
        row.Review.IssueTags,
        row.Review.Status,
        row.Review.IsFlagged,
        row.Review.ModeratorNote,
        row.Review.ModeratedByAdminUserId,
        row.Review.ModeratedAtUtc,
        row.Review.CreatedAtUtc);

    /// <summary>
    /// Delegates to <see cref="CsvWriter"/> (task 133a security pass) rather
    /// than hand-rolling RFC 4180 escaping a second time - the previous
    /// private implementation here was a byte-for-byte duplicate of
    /// <c>ReportingQueryService</c>'s, missing the same CSV/formula-injection
    /// mitigation (a leading <c>=</c>/<c>+</c>/<c>-</c>/<c>@</c> opening a
    /// formula in Excel/Sheets) that <c>CsvWriter</c> now applies. Both
    /// <see cref="ReviewModerationRow.CustomerName"/> and the review's own
    /// text/notes are customer- or moderator-supplied, so this export cannot
    /// skip the same protection every other report gets.
    /// </summary>
    private static byte[] BuildCsv(IReadOnlyList<ReviewModerationRow> rows)
    {
        var header = new[]
        {
            "ReviewId", "BookingId", "CustomerName", "ServiceName", "CategoryName",
            "Rating", "ReviewText", "IssueTags", "Status", "IsFlagged",
            "ModeratorNote", "ModeratedAtUtc", "CreatedAtUtc"
        };

        var rowValues = rows.Select(row => (IReadOnlyList<string?>)new[]
        {
            row.Review.Id.ToString(),
            row.Review.BookingId.ToString(),
            row.CustomerName,
            row.ServiceName,
            row.CategoryName,
            row.Review.Rating.ToString(CultureInfo.InvariantCulture),
            row.Review.ReviewText,
            row.Review.IssueTags,
            row.Review.Status.ToString(),
            row.Review.IsFlagged.ToString(),
            row.Review.ModeratorNote,
            row.Review.ModeratedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            row.Review.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        });

        return CsvWriter.Write(header, rowValues);
    }
}
