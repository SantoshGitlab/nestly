using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Time;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Performance.Tests;

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
}
