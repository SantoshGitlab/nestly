using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "BackgroundJobs" configuration section (T018).
/// </summary>
public class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// When false, the process registers job <em>clients</em> (so application
    /// code can enqueue work) but starts no server, and therefore executes
    /// nothing. Lets the consumer API enqueue jobs that only the admin process
    /// runs, and keeps tests from processing a shared queue.
    /// </summary>
    public bool ServerEnabled { get; set; } = true;

    /// <summary>
    /// Worker threads per server. Defaults to Hangfire's own heuristic when
    /// null, which scales with processor count.
    /// </summary>
    [Range(1, 100)]
    public int? WorkerCount { get; set; }

    /// <summary>
    /// Retry attempts for a failed job before it moves to the failed state.
    /// </summary>
    [Range(0, 10)]
    public int RetryAttempts { get; set; } = 5;

    /// <summary>
    /// Whether to expose the Hangfire dashboard in this process. Only ever
    /// enabled for the admin API — the dashboard can trigger and delete jobs,
    /// so it must never be reachable from the customer-facing surface.
    /// </summary>
    public bool DashboardEnabled { get; set; }

    /// <summary>Path the dashboard is mounted at, when enabled.</summary>
    public string DashboardPath { get; set; } = "/hangfire";
}
