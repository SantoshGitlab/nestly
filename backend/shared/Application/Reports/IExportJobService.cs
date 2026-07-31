using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Reports;

/// <summary>
/// Permission-gated async export queue (SRS 12.18.2, task 128d): an admin
/// requests a report export, a Hangfire background job generates it, and the
/// admin polls/downloads the result rather than a request thread blocking
/// for however long a large export takes.
/// </summary>
public interface IExportJobService
{
    /// <summary>Creates a Pending job and enqueues its background generation. <paramref name="requestedByAdminUserId"/> is the requesting admin, both for audit and so only they can later download the result.</summary>
    Task<Result<ExportJobStatusResponse>> RequestExportAsync(RequestExportJobRequest request, Guid requestedByAdminUserId);

    /// <summary>Status of one export job, scoped to <paramref name="requestedByAdminUserId"/> - returns NotFound for another admin's job rather than Forbidden, so a job id cannot be used to probe for the existence of someone else's export.</summary>
    Task<Result<ExportJobStatusResponse>> GetStatusAsync(Guid jobId, Guid requestedByAdminUserId);

    /// <summary>Every export job <paramref name="requestedByAdminUserId"/> has requested, newest first.</summary>
    Task<Result<IReadOnlyList<ExportJobStatusResponse>>> ListMineAsync(Guid requestedByAdminUserId);

    /// <summary>The generated CSV and its filename, once <see cref="Domain.ExportJobStatus.Completed"/> - fails otherwise.</summary>
    Task<Result<(byte[] Content, string FileName)>> DownloadAsync(Guid jobId, Guid requestedByAdminUserId);

    /// <summary>
    /// Generates the export (the Hangfire job body). Not gated by
    /// <paramref name="requestedByAdminUserId"/> ownership checks - the
    /// background worker, not an HTTP caller, invokes this directly by job
    /// id.
    /// </summary>
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken);
}
