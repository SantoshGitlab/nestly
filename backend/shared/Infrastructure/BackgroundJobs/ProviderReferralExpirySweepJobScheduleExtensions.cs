using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Application.ProviderReferral;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the provider-referral expiry sweep as a Hangfire recurring job,
/// mirrors <see cref="RecurringBookingJobScheduleExtensions"/>.
/// </summary>
public static class ProviderReferralExpirySweepJobScheduleExtensions
{
    /// <summary>Job id Hangfire's storage tracks this recurring registration under - stable across deploys.</summary>
    private const string JobId = "provider-referral-expiry-sweep";

    /// <summary>Call only from the process that actually runs a Hangfire server (see RecurringBookingJobScheduleExtensions' doc comment for why).</summary>
    public static IApplicationBuilder ScheduleProviderReferralExpirySweepJob(this IApplicationBuilder app)
    {
        var backgroundJobOptions = app.ApplicationServices.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
        if (!backgroundJobOptions.ServerEnabled)
        {
            return app;
        }

        var recurringJobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        // Runs once a day, at a low-traffic hour - most invocations on most
        // days will find nothing to sweep.
        recurringJobManager.AddOrUpdate<IProviderReferralExpirySweepService>(
            JobId,
            sweeper => sweeper.SweepAsync(CancellationToken.None),
            Cron.Daily(3));

        return app;
    }
}
