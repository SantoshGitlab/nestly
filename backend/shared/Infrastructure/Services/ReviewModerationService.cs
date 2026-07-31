using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

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

    private static byte[] BuildCsv(IReadOnlyList<ReviewModerationRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', [
            "ReviewId", "BookingId", "CustomerName", "ServiceName", "CategoryName",
            "Rating", "ReviewText", "IssueTags", "Status", "IsFlagged",
            "ModeratorNote", "ModeratedAtUtc", "CreatedAtUtc"
        ]));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', [
                CsvField(row.Review.Id.ToString()),
                CsvField(row.Review.BookingId.ToString()),
                CsvField(row.CustomerName),
                CsvField(row.ServiceName),
                CsvField(row.CategoryName),
                CsvField(row.Review.Rating.ToString(CultureInfo.InvariantCulture)),
                CsvField(row.Review.ReviewText),
                CsvField(row.Review.IssueTags),
                CsvField(row.Review.Status.ToString()),
                CsvField(row.Review.IsFlagged.ToString()),
                CsvField(row.Review.ModeratorNote),
                CsvField(row.Review.ModeratedAtUtc?.ToString("O", CultureInfo.InvariantCulture)),
                CsvField(row.Review.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))
            ]));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Quotes a CSV field only when needed (RFC 4180) - it contains a comma, quote, or newline - doubling any embedded quotes.</summary>
    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuoting = value.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuoting)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
