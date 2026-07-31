using Hangfire;
using Nestly.Application;
using Nestly.Application.Reports;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Permission-gated async export queue (SRS 12.18.2, task 128d): creates an
/// <see cref="ExportJob"/> row and enqueues its generation onto Hangfire
/// (already wired for the platform by task 18's <c>AddBackgroundJobs</c>, but
/// never yet enqueued anywhere - this is its first real job) rather than
/// generating the report on the request thread. Report generation itself is
/// delegated to <see cref="IReportingQueryService"/> - the same CSV logic the
/// instant/synchronous report endpoints use, so a large export produces
/// byte-for-byte the same file an instant one would, just off the request
/// thread and polled for instead of downloaded immediately.
/// </summary>
public sealed class ExportJobService : IExportJobService
{
    private readonly IExportJobRepository _exportJobRepository;
    private readonly IReportingQueryService _reportingQueryService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public ExportJobService(
        IExportJobRepository exportJobRepository,
        IReportingQueryService reportingQueryService,
        IBackgroundJobClient backgroundJobClient)
    {
        _exportJobRepository = exportJobRepository;
        _reportingQueryService = reportingQueryService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<Result<ExportJobStatusResponse>> RequestExportAsync(RequestExportJobRequest request, Guid requestedByAdminUserId)
    {
        var job = new ExportJob(
            Guid.NewGuid(), request.ReportType, request.FromUtc, request.ToUtc,
            request.City, request.CategoryId, requestedByAdminUserId);

        await _exportJobRepository.AddAsync(job);

        // Enqueued by id, not by capturing the job instance - Hangfire
        // serializes the method call for later, out-of-process execution, so
        // the worker that eventually runs it re-reads current state from the
        // database rather than a stale in-memory snapshot.
        _backgroundJobClient.Enqueue<IExportJobService>(x => x.ProcessAsync(job.Id, CancellationToken.None));

        return ToStatusResponse(job);
    }

    public async Task<Result<ExportJobStatusResponse>> GetStatusAsync(Guid jobId, Guid requestedByAdminUserId)
    {
        var job = await _exportJobRepository.GetByIdAsync(jobId);
        if (job is null || job.RequestedByAdminUserId != requestedByAdminUserId)
        {
            return NotFound();
        }

        return ToStatusResponse(job);
    }

    public async Task<Result<IReadOnlyList<ExportJobStatusResponse>>> ListMineAsync(Guid requestedByAdminUserId)
    {
        var jobs = await _exportJobRepository.ListByRequesterAsync(requestedByAdminUserId);
        IReadOnlyList<ExportJobStatusResponse> response = jobs.Select(ToStatusResponse).ToList();
        return Result.Success(response);
    }

    public async Task<Result<(byte[] Content, string FileName)>> DownloadAsync(Guid jobId, Guid requestedByAdminUserId)
    {
        var job = await _exportJobRepository.GetByIdAsync(jobId);
        if (job is null || job.RequestedByAdminUserId != requestedByAdminUserId)
        {
            return NotFound();
        }

        if (job.Status != ExportJobStatus.Completed || job.ResultContent is null || job.ResultFileName is null)
        {
            return Error.Business("ExportJob.NotReady", "This export is not ready to download yet.");
        }

        return (job.ResultContent, job.ResultFileName);
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _exportJobRepository.GetByIdAsync(jobId);
        if (job is null)
        {
            // Nothing sensible to do - the job row that would carry a
            // failure reason does not exist. Should not happen outside of
            // direct DB tampering (Enqueue always follows a successful
            // AddAsync of the same id).
            return;
        }

        job.MarkProcessing();
        await _exportJobRepository.UpdateAsync(job);

        try
        {
            byte[] content = await GenerateCsvAsync(job);
            job.MarkCompleted(BuildFileName(job), content);
        }
        catch (Exception ex)
        {
            // Never persists the raw exception (stack trace, connection
            // details) - only its message, and only as a job-status field an
            // admin already has permission to view (docs/CODING-STANDARDS.md:
            // never expose internals to the caller).
            job.MarkFailed(ex.Message);
        }

        await _exportJobRepository.UpdateAsync(job);
    }

    private async Task<byte[]> GenerateCsvAsync(ExportJob job)
    {
        Result<byte[]> result = job.ReportType switch
        {
            ExportReportType.BookingRevenue => await _reportingQueryService.ExportBookingRevenueCsvAsync(
                new BookingRevenueReportRequest(job.FromUtc, job.ToUtc, job.City, job.CategoryId)),
            ExportReportType.RefundUsage => await _reportingQueryService.ExportRefundCsvAsync(
                new RefundReportRequest(job.FromUtc, job.ToUtc)),
            ExportReportType.CouponUsage => await _reportingQueryService.ExportCouponUsageCsvAsync(
                new CouponUsageReportRequest(job.FromUtc, job.ToUtc)),
            ExportReportType.CustomerSegmentation => await _reportingQueryService.ExportCustomerSegmentationCsvAsync(
                new CustomerSegmentationReportRequest(job.FromUtc, job.ToUtc)),
            ExportReportType.SupportTicket => await _reportingQueryService.ExportSupportTicketCsvAsync(
                new SupportTicketReportRequest(job.FromUtc, job.ToUtc)),
            _ => throw new InvalidOperationException($"Unknown report type '{job.ReportType}'."),
        };

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return result.Value;
    }

    private static string BuildFileName(ExportJob job) =>
        $"{job.ReportType.ToString().ToLowerInvariant()}-export-{job.Id}.csv";

    private static ExportJobStatusResponse ToStatusResponse(ExportJob job) =>
        new(job.Id, job.ReportType, job.Status, job.RequestedAtUtc, job.CompletedAtUtc, job.ErrorMessage, job.ResultContent is not null);

    private static Error NotFound() =>
        Error.NotFound("ExportJob.NotFound", "No export job was found with that id.");
}
