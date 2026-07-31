using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nestly.Application.Reports;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Covers task 128d: the permission-gated async export queue - request,
/// poll, download, and the Hangfire job body (<see cref="ExportJobService.ProcessAsync"/>)
/// itself. <see cref="IBackgroundJobClient"/> is mocked - these tests assert
/// that a job gets enqueued and that <c>ProcessAsync</c> (the job body) does
/// the right thing when invoked directly, not Hangfire's own scheduling,
/// which is out of scope here.
/// </summary>
public sealed class ExportJobServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();

    public ExportJobServiceTests()
    {
        _backgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("1");
    }

    private ExportJobService BuildService(NestlyDbContext context) => new(
        new ExportJobRepository(context),
        new ReportingQueryService(context),
        _backgroundJobClient.Object);

    [Fact]
    public async Task Requesting_an_export_creates_a_pending_job_and_enqueues_it()
    {
        Guid adminUserId = Guid.NewGuid();

        await using var context = _db.CreateContext();
        var result = await BuildService(context).RequestExportAsync(
            new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null),
            adminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ExportJobStatus.Pending);
        result.Value.HasResult.Should().BeFalse();

        _backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);

        var stored = await context.Set<ExportJob>().SingleAsync(j => j.Id == result.Value.Id);
        stored.RequestedByAdminUserId.Should().Be(adminUserId);
    }

    [Fact]
    public async Task Processing_a_job_marks_it_completed_with_csv_content()
    {
        Guid adminUserId = Guid.NewGuid();
        Guid jobId;

        await using (var context = _db.CreateContext())
        {
            var requestResult = await BuildService(context).RequestExportAsync(
                new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null),
                adminUserId);
            jobId = requestResult.Value.Id;
        }

        await using (var context = _db.CreateContext())
        {
            await BuildService(context).ProcessAsync(jobId, CancellationToken.None);
        }

        await using var readContext = _db.CreateContext();
        var status = await BuildService(readContext).GetStatusAsync(jobId, adminUserId);

        status.IsSuccess.Should().BeTrue();
        status.Value.Status.Should().Be(ExportJobStatus.Completed);
        status.Value.HasResult.Should().BeTrue();

        var download = await BuildService(readContext).DownloadAsync(jobId, adminUserId);
        download.IsSuccess.Should().BeTrue();
        download.Value.FileName.Should().EndWith(".csv");
        System.Text.Encoding.UTF8.GetString(download.Value.Content).Should().Contain("Dimension,Value,Count");
    }

    [Fact]
    public async Task Downloading_before_completion_fails_with_not_ready()
    {
        Guid adminUserId = Guid.NewGuid();

        await using var context = _db.CreateContext();
        var requestResult = await BuildService(context).RequestExportAsync(
            new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null),
            adminUserId);

        var download = await BuildService(context).DownloadAsync(requestResult.Value.Id, adminUserId);

        download.IsFailure.Should().BeTrue();
        download.Error.Code.Should().Be("ExportJob.NotReady");
    }

    [Fact]
    public async Task Another_admins_job_is_not_visible_by_id()
    {
        Guid owner = Guid.NewGuid();
        Guid otherAdmin = Guid.NewGuid();

        await using var context = _db.CreateContext();
        var requestResult = await BuildService(context).RequestExportAsync(
            new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null),
            owner);

        var status = await BuildService(context).GetStatusAsync(requestResult.Value.Id, otherAdmin);
        status.IsFailure.Should().BeTrue();
        status.Error.Code.Should().Be("ExportJob.NotFound");

        var download = await BuildService(context).DownloadAsync(requestResult.Value.Id, otherAdmin);
        download.IsFailure.Should().BeTrue();
        download.Error.Code.Should().Be("ExportJob.NotFound");
    }

    [Fact]
    public async Task Listing_mine_excludes_other_admins_jobs()
    {
        Guid owner = Guid.NewGuid();
        Guid otherAdmin = Guid.NewGuid();

        await using var context = _db.CreateContext();
        var service = BuildService(context);
        await service.RequestExportAsync(
            new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null), owner);
        await service.RequestExportAsync(
            new RequestExportJobRequest(ExportReportType.CustomerSegmentation, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, City: null, CategoryId: null), otherAdmin);

        var mine = await service.ListMineAsync(owner);

        mine.IsSuccess.Should().BeTrue();
        mine.Value.Should().HaveCount(1);
    }

    public void Dispose() => _db.Dispose();
}
