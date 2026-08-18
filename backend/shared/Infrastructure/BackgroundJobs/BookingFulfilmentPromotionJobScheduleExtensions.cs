using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers task 333's <c>Confirmed -&gt; AwaitingFulfilment</c> promotion as
/// a Hangfire recurring job - the trigger that finally makes the automatic
/// assignment engine (tasks 246-248) reachable on an ordinary booking. Same
/// <see cref="IApplicationBuilder"/> shape and the same
/// after-<c>WebApplication.Build()</c> reason as
/// <see cref="RecurringBookingJobScheduleExtensions"/>.
/// </summary>
public static class BookingFulfilmentPromotionJobScheduleExtensions
{
    /// <summary>Job id Hangfire's storage tracks this registration under - stable across deploys so re-registering on every startup updates the same entry rather than accumulating duplicates.</summary>
    private const string JobId = "booking-fulfilment-promotion";

    /// <summary>
    /// Every five minutes, matching <c>booking-expiry-sweep</c> rather than
    /// the daily sweeps beside it.
    ///
    /// <para>
    /// The cadence and <see cref="AutoAssignmentOptions.PromotionLeadTimeHours"/>
    /// answer two different questions and both have to be right. The lead
    /// window decides <i>how far ahead of the slot</i> a booking is handed to
    /// the matcher; the cadence decides <i>how late</i> that can happen. For a
    /// booking confirmed well in advance the cadence is irrelevant - it is
    /// promoted within five minutes of entering a 24-hour window, and five
    /// minutes either way of "a day before" means nothing. The cadence exists
    /// for the other case: a booking confirmed <b>inside</b> the window, which
    /// every same-day booking is. Those are already overdue for dispatch the
    /// instant they are paid for, so the interval is the entire delay between
    /// a customer paying at 10:00 for an 11:00 slot and a provider being
    /// offered the job. Five minutes is a defensible worst case there; an
    /// hourly job would be a poor one, and a daily job would miss such
    /// bookings entirely - their slot would have come and gone before the next
    /// pass.
    /// </para>
    /// <para>
    /// The price of the cadence is one indexed query per pass over
    /// <c>booking (status, slot_date)</c>, which returns nothing on the
    /// overwhelming majority of them.
    /// </para>
    /// </summary>
    private const string Schedule = "*/5 * * * *";

    /// <summary>
    /// Call only from the process that actually runs a Hangfire server
    /// (<see cref="BackgroundJobOptions.ServerEnabled"/> - today, only
    /// admin-api), for the reasons spelled out on
    /// <see cref="RecurringBookingJobScheduleExtensions.ScheduleRecurringBookingJob"/>.
    /// </summary>
    /// <remarks>
    /// Registration is unconditional on
    /// <see cref="AutoAssignmentOptions.PromotionEnabled"/> on purpose: the
    /// kill switch is read inside the job, on every pass, so flipping it takes
    /// effect without a restart and flipping it back needs no re-registration.
    /// A disabled pass is one options read.
    /// </remarks>
    public static IApplicationBuilder ScheduleBookingFulfilmentPromotion(this IApplicationBuilder app)
    {
        var backgroundJobOptions = app.ApplicationServices.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
        if (!backgroundJobOptions.ServerEnabled)
        {
            return app;
        }

        var recurringJobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<IBookingFulfilmentPromotionJob>(
            JobId,
            job => job.PromoteDueBookingsAsync(CancellationToken.None),
            Schedule);

        return app;
    }
}
