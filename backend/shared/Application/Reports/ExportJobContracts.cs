using Nestly.Domain;

namespace Nestly.Application.Reports;

/// <summary>
/// Requests an asynchronous report export (SRS 12.18.2 "Large report export
/// may be asynchronous", task 128d). Carries the union of every report's
/// filter fields - <see cref="City"/>/<see cref="CategoryId"/> apply only to
/// <see cref="ExportReportType.BookingRevenue"/>, and <see cref="FromUtc"/>/
/// <see cref="ToUtc"/> are ignored by <see cref="ExportReportType.CustomerSegmentation"/> -
/// rather than one request type per report kind, since the queue itself
/// (create/poll/download) is identical regardless of which report is being
/// generated.
/// </summary>
public sealed record RequestExportJobRequest(
    ExportReportType ReportType, DateTime FromUtc, DateTime ToUtc, string? City, Guid? CategoryId);

public sealed record ExportJobStatusResponse(
    Guid Id,
    ExportReportType ReportType,
    ExportJobStatus Status,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    bool HasResult);
