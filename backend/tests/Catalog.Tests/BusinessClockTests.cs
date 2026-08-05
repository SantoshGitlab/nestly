using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// The business wall-clock. Slot windows and slot dates are stored as local
/// business time with no offset, so every "has this slot passed" decision
/// depends on these conversions being right - measuring a 17:00 window against
/// a UTC instant is what let an IST customer book a slot that had already
/// finished three hours earlier.
/// </summary>
public sealed class BusinessClockTests
{
    private static BusinessClock Build(string timeZoneId, DateTimeOffset utcNow) =>
        new(new FixedTimeProvider(utcNow), Options.Create(new BusinessTimeOptions { TimeZoneId = timeZoneId }));

    [Fact]
    public void Now_is_the_wall_clock_in_the_configured_zone_not_utc()
    {
        // 16:47 UTC is 22:17 in Kolkata (UTC+05:30).
        var clock = Build("Asia/Kolkata", new DateTimeOffset(2026, 8, 5, 16, 47, 0, TimeSpan.Zero));

        clock.Now.Should().Be(new DateTime(2026, 8, 5, 22, 17, 0));
        clock.Today.Should().Be(new DateOnly(2026, 8, 5));
    }

    [Fact]
    public void Today_rolls_over_on_the_business_day_not_the_utc_day()
    {
        // 20:00 UTC on the 5th is already 01:30 on the 6th in Kolkata.
        var clock = Build("Asia/Kolkata", new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero));

        clock.Today.Should().Be(new DateOnly(2026, 8, 6));
    }

    [Fact]
    public void ToUtc_lifts_a_stored_slot_time_to_the_instant_it_actually_occurs()
    {
        var clock = Build("Asia/Kolkata", DateTimeOffset.UnixEpoch);

        var slotStart = clock.ToUtc(new DateOnly(2026, 8, 6), TimeSpan.FromHours(9));

        // 09:00 IST is 03:30 UTC the same day.
        slotStart.Should().Be(new DateTime(2026, 8, 6, 3, 30, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// The regression this abstraction exists for: at 22:17 local, a window
    /// that started at 17:00 the same day is in the past. Comparing the raw
    /// snapshot against UTC now made it look 5.5 hours in the future.
    /// </summary>
    [Fact]
    public void A_window_that_started_earlier_today_is_in_the_past_against_utc_now()
    {
        var utcNow = new DateTimeOffset(2026, 8, 5, 16, 47, 0, TimeSpan.Zero);
        var clock = Build("Asia/Kolkata", utcNow);

        var windowStart = clock.ToUtc(new DateOnly(2026, 8, 5), TimeSpan.FromHours(17));

        windowStart.Should().BeBefore(utcNow.UtcDateTime);
        clock.Now.Should().BeAfter(new DateTime(2026, 8, 5, 17, 0, 0));
    }

    [Fact]
    public void An_unknown_timezone_id_fails_fast_rather_than_silently_using_utc()
    {
        var act = () => Build("Mars/Olympus_Mons", DateTimeOffset.UnixEpoch);

        act.Should().Throw<TimeZoneNotFoundException>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
