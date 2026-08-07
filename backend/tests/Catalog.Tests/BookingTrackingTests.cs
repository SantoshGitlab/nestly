using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 271's tracking row: the recompute throttle it answers, the
/// material-change rule that decides whether an ETA is news, and the clearing
/// it does when a job stops being trackable.
/// </summary>
/// <remarks>
/// The persistence half runs against the in-memory SQLite
/// <see cref="TestDatabase"/>, which builds its schema with
/// <c>EnsureCreated</c> from <c>BookingTrackingConfiguration</c> and never
/// runs migrations. So the migration added alongside this entity is NOT
/// exercised here - PostgreSQL gets its table from the migration, these tests
/// get theirs from the EF configuration, and the two agree only because the
/// migration was generated from that same configuration. A hand-edit to either
/// one alone would pass this suite and break production.
/// </remarks>
public class BookingTrackingTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private const decimal OriginLatitude = 12.9716m;
    private const decimal OriginLongitude = 77.5946m;

    /// <summary>Roughly 100 m north of the origin - inside any sane movement threshold.</summary>
    private const decimal BarelyMovedLatitude = 12.9725m;

    /// <summary>Roughly 2 km north of the origin.</summary>
    private const decimal FarMovedLatitude = 12.9896m;

    private static BookingTracking NewTracking(Guid? bookingId = null) =>
        new(Guid.NewGuid(), bookingId ?? Guid.NewGuid());

    private static void ApplyInitialEta(BookingTracking tracking, int etaSeconds, DateTime computedAtUtc)
    {
        tracking.ApplyEta(
            Guid.NewGuid(), etaSeconds, 4_000, BookingEtaSource.GoogleMaps,
            OriginLatitude, OriginLongitude, computedAtUtc);
        tracking.ClearDomainEvents();
    }

    [Fact]
    public void Constructor_rejects_an_empty_booking()
    {
        var act = () => new BookingTracking(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_row_with_no_estimate_yet_always_recomputes()
    {
        var tracking = NewTracking();

        tracking.HasEta.Should().BeFalse();
        tracking.ShouldRecompute(DateTime.UtcNow, OriginLatitude, OriginLongitude, TimeSpan.FromHours(1), 100_000m)
            .Should().BeTrue("the first ETA of a job is the one the customer is waiting for, and there is nothing to throttle against");
    }

    [Fact]
    public void A_recent_estimate_from_the_same_place_is_not_recomputed()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        tracking.ShouldRecompute(
                computedAtUtc.AddSeconds(59), OriginLatitude, OriginLongitude,
                TimeSpan.FromSeconds(60), 250m)
            .Should().BeFalse();
    }

    [Fact]
    public void An_estimate_older_than_the_interval_is_recomputed_even_from_the_same_place()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        tracking.ShouldRecompute(
                computedAtUtc.AddSeconds(60), OriginLatitude, OriginLongitude,
                TimeSpan.FromSeconds(60), 250m)
            .Should().BeTrue();
    }

    [Fact]
    public void A_barely_moved_provider_is_not_recomputed_inside_the_interval()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        tracking.ShouldRecompute(
                computedAtUtc.AddSeconds(1), BarelyMovedLatitude, OriginLongitude,
                TimeSpan.FromSeconds(60), 250m)
            .Should().BeFalse("100 m is GPS scatter and a parked van, not a journey");
    }

    [Fact]
    public void A_provider_who_has_covered_ground_is_recomputed_inside_the_interval()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        tracking.ShouldRecompute(
                computedAtUtc.AddSeconds(1), FarMovedLatitude, OriginLongitude,
                TimeSpan.FromSeconds(60), 250m)
            .Should().BeTrue("2 km makes the stored estimate wrong regardless of how recently it was computed");
    }

    [Fact]
    public void The_movement_gate_measures_from_where_the_estimate_was_computed_not_from_the_previous_fix()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        // Three consecutive 100 m hops are each individually under the
        // threshold but together put the provider 300 m from the estimate's
        // origin, which is exactly when the stored ETA has gone stale.
        tracking.ShouldRecompute(computedAtUtc, 12.9743m, OriginLongitude, TimeSpan.FromSeconds(60), 250m)
            .Should().BeTrue();
    }

    [Fact]
    public void The_first_estimate_is_always_announced()
    {
        var tracking = NewTracking();

        tracking.ApplyEta(
            Guid.NewGuid(), 600, 4_000, BookingEtaSource.GoogleMaps,
            OriginLatitude, OriginLongitude, DateTime.UtcNow);

        tracking.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BookingEtaUpdatedEvent>();
    }

    [Theory]
    [InlineData(660)]
    [InlineData(540)]
    [InlineData(60)]
    public void A_material_change_is_announced(int newEtaSeconds)
    {
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, DateTime.UtcNow);

        tracking.ApplyEta(
            Guid.NewGuid(), newEtaSeconds, 4_000, BookingEtaSource.GoogleMaps,
            OriginLatitude, OriginLongitude, DateTime.UtcNow);

        tracking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingEtaUpdatedEvent>()
            .Which.EtaSeconds.Should().Be(newEtaSeconds);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(659)]
    [InlineData(541)]
    public void Jitter_is_not_announced(int newEtaSeconds)
    {
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, DateTime.UtcNow);

        tracking.ApplyEta(
            Guid.NewGuid(), newEtaSeconds, 4_000, BookingEtaSource.GoogleMaps,
            OriginLatitude, OriginLongitude, DateTime.UtcNow);

        tracking.DomainEvents.Should().BeEmpty(
            "every surface renders whole minutes, so a sub-minute wobble cannot tell the customer anything new");
    }

    [Fact]
    public void An_immaterial_change_still_moves_the_throttle_baseline()
    {
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, computedAtUtc);

        var laterUtc = computedAtUtc.AddSeconds(70);
        tracking.ApplyEta(
            Guid.NewGuid(), 605, 3_900, BookingEtaSource.GoogleMaps,
            FarMovedLatitude, OriginLongitude, laterUtc);

        // The announcement is suppressed but the row is not frozen: leaving the
        // old timestamp and origin behind would make the next throttle decision
        // fire off a position and a time the system has already moved past.
        tracking.DomainEvents.Should().BeEmpty();
        tracking.EtaSeconds.Should().Be(605);
        tracking.EtaComputedAtUtc.Should().Be(laterUtc);
        tracking.EtaOriginLatitude.Should().Be(FarMovedLatitude);
    }

    [Fact]
    public void Clearing_drops_the_whole_estimate_and_announces_nothing()
    {
        var tracking = NewTracking();
        ApplyInitialEta(tracking, 600, DateTime.UtcNow);

        tracking.ClearEta().Should().BeTrue();

        tracking.HasEta.Should().BeFalse();
        tracking.EtaSeconds.Should().BeNull();
        tracking.EtaDistanceMetres.Should().BeNull();
        tracking.EtaComputedAtUtc.Should().BeNull();
        tracking.EtaSource.Should().BeNull();
        tracking.EtaOriginLatitude.Should().BeNull();
        tracking.EtaOriginLongitude.Should().BeNull();
        tracking.DomainEvents.Should().BeEmpty(
            "BookingEtaUpdatedEvent carries a non-nullable EtaSeconds and cannot express 'there is no longer an ETA'");
    }

    [Fact]
    public void Clearing_an_already_empty_row_reports_that_there_was_nothing_to_write()
    {
        var tracking = NewTracking();

        tracking.ClearEta().Should().BeFalse();
    }

    [Fact]
    public async Task An_estimate_round_trips_through_the_database()
    {
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var computedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        await using (var writeContext = _database.CreateContext())
        {
            var tracking = new BookingTracking(Guid.NewGuid(), bookingId);
            tracking.ApplyEta(providerId, 615, 4_250, BookingEtaSource.Sandbox, OriginLatitude, OriginLongitude, computedAtUtc);
            await new BookingTrackingRepository(writeContext).AddAsync(tracking);
        }

        await using var readContext = _database.CreateContext();
        var stored = await new BookingTrackingRepository(readContext).GetByBookingAsync(bookingId);

        stored.Should().NotBeNull();
        stored!.ProviderId.Should().Be(providerId);
        stored.EtaSeconds.Should().Be(615);
        stored.EtaDistanceMetres.Should().Be(4_250);
        stored.EtaSource.Should().Be(BookingEtaSource.Sandbox);
        stored.EtaComputedAtUtc.Should().Be(computedAtUtc);
        stored.EtaOriginLatitude.Should().Be(OriginLatitude);
        stored.EtaOriginLongitude.Should().Be(OriginLongitude);
    }

    [Fact]
    public async Task A_booking_cannot_end_up_with_two_tracking_rows()
    {
        var bookingId = Guid.NewGuid();

        await using var context = _database.CreateContext();
        var repository = new BookingTrackingRepository(context);
        await repository.AddAsync(new BookingTracking(Guid.NewGuid(), bookingId));

        var act = async () => await repository.AddAsync(new BookingTracking(Guid.NewGuid(), bookingId));

        await act.Should().ThrowAsync<DbUpdateException>(
            "two rows would give the customer read model and the admin live-ops list different ETAs for the same job");
    }

    public void Dispose() => _database.Dispose();
}
