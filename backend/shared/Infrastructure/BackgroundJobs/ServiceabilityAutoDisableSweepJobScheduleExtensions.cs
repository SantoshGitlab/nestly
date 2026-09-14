using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Application.Serviceability;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers <see cref="IServiceabilityAutoDisableSweepJob"/> as a Hangfire
/// recurring job - the periodic backstop for the auto-disable grace period
/// (see the interface's doc comment). Same <see cref="IApplicationBuilder"/>
/// shape as <see cref="BookingFulfilmentPromotionJobScheduleExtensions"/>.
/// </summary>
public static class ServiceabilityAutoDisableSweepJobScheduleExtensions
{
    /// <summary>Job id Hangfire's storage tracks this recurring registration under - stable across deploys.</summary>
    private const string JobId = "serviceability-auto-disable-sweep";

    /// <summary>
    /// Every five minutes - a fraction of the
    /// <see cref="ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes"/>
    /// grace period, same cadence reasoning as <c>booking-fulfilment-promotion</c>:
    /// cheap enough to run often (one indexed query over mostly-empty
    /// results), and frequent enough that a mapping is disabled within
    /// minutes of its grace period elapsing rather than up to a full sweep
    /// interval late.
    /// </summary>
    private const string Schedule = "*/5 * * * *";

    /// <summary>
    /// Call only from the process that actually runs a Hangfire server
    /// (<see cref="BackgroundJobOptions.ServerEnabled"/> - today, only
    /// admin-api).
    /// </summary>
    /// <remarks>
    /// Registration is unconditional on <c>FeatureFlagSettings.AutoManageServiceabilityEnabled</c>
    /// on purpose - the kill switch is read inside the job, on every pass, so
    /// flipping it takes effect without a restart and flipping it back needs
    /// no re-registration.
    /// </remarks>
    public static IApplicationBuilder ScheduleServiceabilityAutoDisableSweepJob(this IApplicationBuilder app)
    {
        var backgroundJobOptions = app.ApplicationServices.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
        if (!backgroundJobOptions.ServerEnabled)
        {
            return app;
        }

        var recurringJobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<IServiceabilityAutoDisableSweepJob>(
            JobId,
            job => job.SweepAsync(CancellationToken.None),
            Schedule);

        return app;
    }
}
