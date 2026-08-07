using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 268: the append-only location trail's three reads. Every test
/// writes its pings out of chronological order, because an ordering assertion
/// against rows inserted in order passes whether or not the query orders
/// anything.
/// </summary>
/// <remarks>
/// Runs on the in-memory SQLite fixture, which is built with
/// <c>EnsureCreated</c> and so never executes the migration - the schema here
/// comes from the same entity configuration, not from
/// <c>20260807020646_AddProviderLocationPing</c>. Two consequences worth
/// knowing: the two indexes exist in PostgreSQL but are irrelevant to a
/// correctness assertion on this fixture, and SQLite stores decimals as text
/// rather than <c>numeric(9,6)</c>, so coordinate precision is enforced only
/// by the real database. Neither affects what these tests assert - ordering
/// and filtering - which behave identically on both.
/// <para>
/// One caveat, verified by deleting the repository's ordering and re-running:
/// <see cref="GetLatestForBookingAsync_returns_the_newest_fix_by_recorded_time_not_by_insert_order"/>
/// fails without it, but the trail test does not, because SQLite satisfies
/// the <c>BookingId</c> filter from the (BookingId, RecordedAtUtc) index and
/// hands the rows back already sorted. The trail assertion still states the
/// contract; it simply cannot be falsified on an engine that picks that plan,
/// so do not read its passing as proof the <c>OrderBy</c> is present.
/// </para>
/// </remarks>
public sealed class ProviderLocationPingRepositoryTests : IClassFixture<TestDatabase>
{
    private static readonly DateTime BaseTimeUtc = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _db;

    public ProviderLocationPingRepositoryTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task An_appended_ping_is_readable_as_the_bookings_latest_fix()
    {
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 0, latitude: 12.9716m));

        var latest = await repository.GetLatestForBookingAsync(bookingId);
        latest.Should().NotBeNull();
        latest!.ProviderId.Should().Be(providerId);
        latest.Latitude.Should().Be(12.9716m);
        latest.Source.Should().Be(ProviderLocationSource.ProviderApp);
    }

    [Fact]
    public async Task GetLatestForBookingAsync_returns_the_newest_fix_by_recorded_time_not_by_insert_order()
    {
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        // The newest fix is written neither first nor last, so an unordered
        // query gives the wrong answer whichever row the database happens to
        // hand back - which is the bug this pins.
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 0, latitude: 12.9716m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 10, latitude: 12.9800m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 5, latitude: 12.9750m));

        var latest = await repository.GetLatestForBookingAsync(bookingId);

        latest!.RecordedAtUtc.Should().Be(BaseTimeUtc.AddMinutes(10));
        latest.Latitude.Should().Be(12.9800m);
    }

    [Fact]
    public async Task GetLatestForBookingAsync_returns_null_before_any_fix_has_arrived()
    {
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        (await repository.GetLatestForBookingAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetTrailForBookingAsync_returns_the_fixes_oldest_first()
    {
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 5, latitude: 12.9750m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 10, latitude: 12.9800m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 0, latitude: 12.9716m));

        var trail = await repository.GetTrailForBookingAsync(bookingId);

        trail.Select(ping => ping.RecordedAtUtc).Should().ContainInOrder(
            BaseTimeUtc, BaseTimeUtc.AddMinutes(5), BaseTimeUtc.AddMinutes(10));
    }

    [Fact]
    public async Task A_bookings_trail_excludes_idle_pings_from_the_same_provider()
    {
        // An idle ping (no BookingId) is the provider's location while not on
        // a job. Leaking one into a booking's trail would draw the customer a
        // route the provider never took for them.
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        await repository.AddAsync(NewPing(providerId, bookingId: null, minutesOffset: 20, latitude: 13.0500m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 0, latitude: 12.9716m));

        var trail = await repository.GetTrailForBookingAsync(bookingId);
        var latest = await repository.GetLatestForBookingAsync(bookingId);

        trail.Should().ContainSingle().Which.Latitude.Should().Be(12.9716m);
        latest!.Latitude.Should().Be(12.9716m);
    }

    [Fact]
    public async Task A_bookings_trail_excludes_another_bookings_fixes()
    {
        var bookingId = Guid.NewGuid();
        var otherBookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        using var context = _db.CreateContext();
        var repository = new ProviderLocationPingRepository(context);

        await repository.AddAsync(NewPing(providerId, otherBookingId, minutesOffset: 30, latitude: 13.1000m));
        await repository.AddAsync(NewPing(providerId, bookingId, minutesOffset: 0, latitude: 12.9716m));

        var trail = await repository.GetTrailForBookingAsync(bookingId);

        trail.Should().ContainSingle().Which.BookingId.Should().Be(bookingId);
    }

    private static ProviderLocationPing NewPing(Guid providerId, Guid? bookingId, int minutesOffset, decimal latitude) =>
        new(
            Guid.NewGuid(),
            providerId,
            bookingId,
            latitude,
            77.5946m,
            8.5m,
            BaseTimeUtc.AddMinutes(minutesOffset),
            BaseTimeUtc.AddMinutes(minutesOffset).AddSeconds(2));
}
