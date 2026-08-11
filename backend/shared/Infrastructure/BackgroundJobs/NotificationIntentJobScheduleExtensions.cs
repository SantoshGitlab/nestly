using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Application.Notifications;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers task 294's notification-intent sweep as a Hangfire recurring
/// job - the retry path that makes a customer notification survive the death
/// of the process that was supposed to send it. Same
/// <see cref="IApplicationBuilder"/> shape and the same
/// after-<c>WebApplication.Build()</c> reason as
/// <see cref="RecurringBookingJobScheduleExtensions"/>.
/// </summary>
public static class NotificationIntentJobScheduleExtensions
{
    /// <summary>Job id Hangfire's storage tracks this registration under - stable across deploys so re-registering on every startup updates the same entry rather than accumulating duplicates.</summary>
    private const string JobId = "notification-intent-sweep";

    /// <summary>
    /// Every two minutes, not daily.
    ///
    /// <para>
    /// This is a latency budget, not a throughput one: the interval plus
    /// <see cref="NotificationIntentOptions.GraceSeconds"/> is the worst case
    /// delay on a notification whose in-process dispatch died, and these are
    /// messages like "your payment failed" and "your professional has changed"
    /// where an hour late is close to useless. A pass that finds nothing - the
    /// overwhelming majority of them - is one indexed query over pending rows.
    /// </para>
    /// </summary>
    private const string Schedule = "*/2 * * * *";

    /// <summary>
    /// Call only from the process that actually runs a Hangfire server
    /// (<see cref="BackgroundJobOptions.ServerEnabled"/> - today, only
    /// admin-api), for the reasons spelled out on
    /// <see cref="RecurringBookingJobScheduleExtensions.ScheduleRecurringBookingJob"/>.
    /// </summary>
    /// <remarks>
    /// The sweep is safe to run on several instances at once regardless -
    /// every intent is taken by a conditional UPDATE before anything is sent -
    /// so widening this beyond one process later needs no change here.
    /// </remarks>
    public static IApplicationBuilder ScheduleNotificationIntentSweep(this IApplicationBuilder app)
    {
        var backgroundJobOptions = app.ApplicationServices.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
        if (!backgroundJobOptions.ServerEnabled)
        {
            return app;
        }

        var recurringJobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<INotificationIntentSweepJob>(
            JobId,
            sweep => sweep.SweepAsync(CancellationToken.None),
            Schedule);

        return app;
    }
}
