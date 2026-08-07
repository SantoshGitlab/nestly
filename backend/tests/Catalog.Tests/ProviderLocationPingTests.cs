using System.Reflection;
using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Pure domain rules on <see cref="ProviderLocationPing"/> (task 268). No
/// database involved - these are invariants the entity enforces regardless of
/// persistence, which matters because its input comes straight off a device.
/// </summary>
public class ProviderLocationPingTests
{
    private static ProviderLocationPing NewPing(
        Guid? providerId = null,
        Guid? bookingId = null,
        decimal latitude = 12.9716m,
        decimal longitude = 77.5946m,
        decimal? accuracyMetres = 8.5m) =>
        new(
            Guid.NewGuid(),
            providerId ?? Guid.NewGuid(),
            bookingId,
            latitude,
            longitude,
            accuracyMetres,
            DateTime.UtcNow.AddSeconds(-3),
            DateTime.UtcNow);

    [Fact]
    public void A_ping_records_the_device_time_and_the_server_time_separately()
    {
        var recordedAtUtc = new DateTime(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc);
        var receivedAtUtc = recordedAtUtc.AddMinutes(4);

        var ping = new ProviderLocationPing(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12.9716m, 77.5946m, 8.5m, recordedAtUtc, receivedAtUtc);

        ping.RecordedAtUtc.Should().Be(recordedAtUtc);
        ping.ReceivedAtUtc.Should().Be(receivedAtUtc);
        ping.Source.Should().Be(ProviderLocationSource.ProviderApp);
    }

    [Fact]
    public void A_fix_delivered_out_of_order_is_still_accepted()
    {
        // A device with a skewed clock can report a "future" recordedAt. The
        // entity keeps both stamps so a reader can see the gap; how much skew
        // to tolerate is the ingest endpoint's policy, not an invariant here.
        var receivedAtUtc = new DateTime(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc);

        Action act = () => new ProviderLocationPing(
            Guid.NewGuid(), Guid.NewGuid(), null, 12.9716m, 77.5946m, null, receivedAtUtc.AddMinutes(2), receivedAtUtc);

        act.Should().NotThrow();
    }

    [Fact]
    public void An_idle_ping_carries_no_booking_and_no_accuracy()
    {
        var ping = NewPing(bookingId: null, accuracyMetres: null);

        ping.BookingId.Should().BeNull();
        ping.AccuracyMetres.Should().BeNull();
    }

    [Fact]
    public void A_ping_without_a_provider_is_rejected()
    {
        Action act = () => NewPing(providerId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(90.000001, 77.5946)]
    [InlineData(-90.000001, 77.5946)]
    [InlineData(12.9716, 180.000001)]
    [InlineData(12.9716, -180.000001)]
    public void A_coordinate_outside_the_world_is_rejected(double latitude, double longitude)
    {
        Action act = () => NewPing(latitude: (decimal)latitude, longitude: (decimal)longitude);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_negative_accuracy_is_rejected()
    {
        Action act = () => NewPing(accuracyMetres: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_ping_exposes_no_way_to_change_it_once_created()
    {
        // The trail is append-only: a fix that can be rewritten after the fact
        // is not evidence of anything. EF writes through the private setters,
        // so this asserts nothing outside the entity can.
        var settable = typeof(ProviderLocationPing)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name);

        settable.Should().BeEmpty();
    }
}
