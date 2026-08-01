using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Infrastructure.Observability;
using Nestly.Infrastructure.Options;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 137a-c's failure-rate alerting: <see cref="NestlyMetricsService"/>
/// should only raise its structured error-level alert once a rolling window
/// has both enough samples to be meaningful and a failure rate at or above
/// the configured threshold - and each metric category's window (payment,
/// booking, one monitor per notification channel) must be independent of the
/// others.
/// </summary>
public sealed class NestlyMetricsServiceTests
{
    private static NestlyMetricsService BuildService(MutableTimeProvider timeProvider, CapturingLogger logger, MetricsOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()), timeProvider, logger);

    private static MetricsOptions DefaultOptions() => new()
    {
        FailureRateWindowMinutes = 5,
        MinimumSamplesForAlert = 4,
        PaymentFailureRateThreshold = 0.5,
        BookingFailureRateThreshold = 0.5,
        NotificationFailureRateThreshold = 0.5
    };

    [Fact]
    public void RecordPaymentOutcome_does_not_alert_before_the_minimum_sample_count_is_reached()
    {
        var logger = new CapturingLogger();
        var service = BuildService(new MutableTimeProvider(DateTimeOffset.UtcNow), logger);

        // 3 failures, 0 successes - a 100% failure rate, but only 3 samples
        // against a MinimumSamplesForAlert of 4, so no alert yet.
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void RecordPaymentOutcome_alerts_once_the_rolling_failure_rate_crosses_the_threshold()
    {
        var logger = new CapturingLogger();
        var service = BuildService(new MutableTimeProvider(DateTimeOffset.UtcNow), logger);

        // 2 successes, 2 failures - 4 samples (meets the minimum) at exactly
        // a 50% failure rate, which meets (>=) the 0.5 threshold.
        service.RecordPaymentOutcome(true, TimeSpan.FromMilliseconds(10));
        service.RecordPaymentOutcome(true, TimeSpan.FromMilliseconds(10));
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.EventId.Should().Be(MetricsAlertEvents.PaymentFailureRateExceeded);
        entry.Message.Should().Contain("payment failure rate");
    }

    [Fact]
    public void RecordBookingCreated_alerts_with_the_booking_specific_event_id()
    {
        var logger = new CapturingLogger();
        var service = BuildService(new MutableTimeProvider(DateTimeOffset.UtcNow), logger);

        service.RecordBookingCreated(true);
        service.RecordBookingCreated(true);
        service.RecordBookingCreated(false, "Booking.SlotCapacityReached");
        service.RecordBookingCreated(false, "Booking.SlotCapacityReached");

        logger.Entries.Should().ContainSingle();
        logger.Entries.Single().EventId.Should().Be(MetricsAlertEvents.BookingFailureRateExceeded);
    }

    [Fact]
    public void RecordNotificationOutcome_tracks_failure_rate_independently_per_channel()
    {
        var logger = new CapturingLogger();
        var service = BuildService(new MutableTimeProvider(DateTimeOffset.UtcNow), logger);

        // Sms crosses the threshold; Email stays entirely healthy. Only the
        // Sms channel's outage should be alert-worthy - a shared/aggregate
        // monitor would either miss this (diluted by Email's healthy sends)
        // or falsely blame Email too.
        service.RecordNotificationOutcome("Sms", false, "Provider.Timeout");
        service.RecordNotificationOutcome("Sms", false, "Provider.Timeout");
        service.RecordNotificationOutcome("Sms", true);
        service.RecordNotificationOutcome("Sms", false, "Provider.Timeout");

        for (int i = 0; i < 10; i++)
        {
            service.RecordNotificationOutcome("Email", true);
        }

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.EventId.Should().Be(MetricsAlertEvents.NotificationFailureRateExceeded);
        entry.Message.Should().Contain("Sms");
    }

    [Fact]
    public void RecordPaymentOutcome_stops_alerting_once_new_successes_dilute_the_rate_back_below_threshold()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var logger = new CapturingLogger();
        var service = BuildService(timeProvider, logger);

        for (int i = 0; i < 4; i++)
        {
            service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        }

        logger.Entries.Should().NotBeEmpty("a 100% failure rate over the minimum sample count should have alerted");

        // Enough new successes to comfortably dilute the rolling window well
        // below the 50% threshold (4 failures against 20 total samples =
        // 20%) - the rate crosses back under threshold gradually, so some of
        // these calls may still alert while still at/above 50%; what matters
        // is that alerting has genuinely stopped by the end.
        for (int i = 0; i < 16; i++)
        {
            service.RecordPaymentOutcome(true, TimeSpan.FromMilliseconds(10));
        }

        int entriesBeforeFinalCall = logger.Entries.Count;
        service.RecordPaymentOutcome(true, TimeSpan.FromMilliseconds(10));

        logger.Entries.Should().HaveCount(entriesBeforeFinalCall, "the failure rate has recovered well below threshold, so no new alert should fire");
    }

    [Fact]
    public void RecordPaymentOutcome_excludes_samples_that_have_aged_out_of_the_rolling_window()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var logger = new CapturingLogger();
        var options = DefaultOptions();
        options.FailureRateWindowMinutes = 5;
        var service = BuildService(timeProvider, logger, options);

        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        service.RecordPaymentOutcome(false, TimeSpan.FromMilliseconds(10), "Gateway.Declined");
        logger.Entries.Should().BeEmpty("only 3 samples so far, below the minimum of 4");

        // Push those 3 failures outside the 5-minute window before recording
        // a 4th sample - the window should now contain only the new sample,
        // not enough to reach the minimum, so still no alert.
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        service.RecordPaymentOutcome(true, TimeSpan.FromMilliseconds(10));

        logger.Entries.Should().BeEmpty("the earlier failures aged out of the rolling window");
    }

    /// <summary>Records every LogError/LogWarning/... call made through it, for assertions Verify-based mocking would otherwise need a library for.</summary>
    private sealed class CapturingLogger : ILogger<NestlyMetricsService>
    {
        public List<(LogLevel LogLevel, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId, formatter(state, exception)));
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
