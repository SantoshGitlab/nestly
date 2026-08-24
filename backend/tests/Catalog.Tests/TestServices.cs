using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.ProviderManagement;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Shared construction of the collaborators that several services now take,
/// so a new cross-cutting dependency (a clock, an options binding) is added in
/// one place rather than in every <c>BuildService</c> helper in the suite.
/// </summary>
internal static class TestServices
{
    /// <summary>
    /// The business clock, pinned to UTC. Test fixtures build their slot dates
    /// from <see cref="DateTime.UtcNow"/>, so a UTC business timezone keeps
    /// "now" and "the slot" on the same clock and leaves each test measuring
    /// the rule it is actually about rather than a timezone offset. Production
    /// runs on the configured <see cref="BusinessTimeOptions.TimeZoneId"/>;
    /// the offset behaviour itself is covered by BusinessClockTests.
    /// </summary>
    public static IBusinessClock Clock(TimeProvider? timeProvider = null) =>
        new BusinessClock(
            timeProvider ?? TimeProvider.System,
            Options.Create(new BusinessTimeOptions { TimeZoneId = "UTC" }));

    /// <summary>
    /// The provider-queue early-release model's occupancy calculator (a
    /// verified-complete, non-duration-based job's actual finish time rather
    /// than its slot's own end), over <see cref="Clock"/> so it shares the
    /// same UTC-pinned "now" every other collaborator in this file does.
    /// </summary>
    public static IProviderJobOccupancyService Occupancy(TimeProvider? timeProvider = null) =>
        new ProviderJobOccupancyService(Clock(timeProvider));

    /// <summary>The provider-queue model's one-active-job gate, for suites that construct <see cref="ProviderJobService"/> directly.</summary>
    public static IProviderActiveJobLimitService ActiveJobLimit(NestlyDbContext context) =>
        new ProviderActiveJobLimitService(context);

    /// <summary>
    /// The provider-queue model's overrun-reassignment follow-up, for suites
    /// that construct <see cref="ProviderJobService"/> directly and are not
    /// themselves testing the overrun rule. Sandbox-backed travel feasibility
    /// (no network, no key) so it never fires for a suite whose bookings share
    /// one address.
    /// </summary>
    public static IOverrunReassignmentService OverrunReassignment(NestlyDbContext context) =>
        new OverrunReassignmentService(
            context,
            new BookingRepository(context),
            new BookingProviderAssignmentRepository(context),
            TravelFeasibilityFactory.Sandbox(context),
            NullLogger<OverrunReassignmentService>.Instance);

    public static SlotAvailabilityService SlotAvailability(NestlyDbContext context, TimeProvider? timeProvider = null) =>
        new(
            new ServiceabilityRepository(context),
            new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService()),
            new SlotWindowRepository(context),
            new SlotBlackoutRepository(context),
            new SlotBookingPolicyRepository(context),
            new SlotCapacityRepository(context),
            Clock(timeProvider));

    public static IOptions<BookingOptions> BookingOptions(int? maxQuantityPerBooking = null) =>
        Options.Create(maxQuantityPerBooking is null
            ? new BookingOptions()
            : new BookingOptions { MaxQuantityPerBooking = maxQuantityPerBooking.Value });

    /// <summary>
    /// The <see cref="IOptionsMonitor{TOptions}"/> equivalent of
    /// <see cref="Options.Create{TOptions}"/> (task 276), which
    /// Microsoft.Extensions.Options does not ship. Only
    /// <c>BookingNotificationTriggerHandler</c> needs it today - it reads its
    /// mute switches through a monitor so an ops mute takes effect on config
    /// reload rather than at the next restart.
    /// </summary>
    public static IOptionsMonitor<T> Monitor<T>(T value) where T : class =>
        new StaticOptionsMonitor<T>(value);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        /// <summary>Nothing ever changes, so the "changed" callback is never invoked - a no-op disposable is the honest registration.</summary>
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// The real task 294 coordinator over the test database. Real, not a stub:
    /// the claim it performs <i>is</i> the deduplication guarantee, so a test
    /// that swapped it for a pass-through would stop covering the one thing
    /// this collaborator exists for. With no intent rows present it fails open
    /// and dispatches, which is exactly how the handlers behaved before task
    /// 294 - that is what keeps the pre-existing trigger-wiring tests
    /// measuring what they always measured.
    /// </summary>
    public static NotificationIntentCoordinator IntentCoordinator(
        NestlyDbContext context,
        NotificationIntentOptions? options = null,
        TimeProvider? timeProvider = null) =>
        new(
            new NotificationIntentRepository(context),
            Monitor(options ?? new NotificationIntentOptions()),
            timeProvider ?? TimeProvider.System,
            NullLogger<NotificationIntentCoordinator>.Instance);

    /// <summary>
    /// Fulfilment notification switches, all enabled unless a test names the
    /// ones it wants muted (task 276).
    /// </summary>
    public static IOptionsMonitor<FulfilmentNotificationOptions> FulfilmentNotifications(
        bool providerAssigned = true,
        bool providerEnRoute = true,
        bool providerArrived = true,
        bool jobStarted = true,
        bool jobCompleted = true,
        bool providerChanged = true) =>
        Monitor(new FulfilmentNotificationOptions
        {
            ProviderAssignedEnabled = providerAssigned,
            ProviderEnRouteEnabled = providerEnRoute,
            ProviderArrivedEnabled = providerArrived,
            JobStartedEnabled = jobStarted,
            JobCompletedEnabled = jobCompleted,
            ProviderChangedEnabled = providerChanged
        });
}
