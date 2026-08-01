using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Observability;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Observability;

/// <summary>
/// <see cref="IMetricsService"/> over <c>System.Diagnostics.Metrics</c>
/// (tasks 137a-c, SRS 29.6; DEVOPS.md OBSERVABILITY). Every instrument is
/// created on <see cref="MeterName"/>, which <c>DependencyInjection.AddInfrastructure</c>
/// registers with the OpenTelemetry SDK's Prometheus exporter - a
/// self-hosted <c>/metrics</c> scrape endpoint (see each API's Program.cs)
/// rather than an OTLP push to a collector, since no OTel collector exists
/// anywhere in this repo's docker-compose yet (DEVOPS.md OPEN DECISIONS
/// still lists the monitoring/alerting stack as unresolved) - a scrape
/// endpoint is the smallest infrastructure footprint that is still
/// immediately useful once that stack lands.
///
/// Registered as a singleton (see AddInfrastructure): both the <see cref="Meter"/>
/// and the failure-rate monitors below need to accumulate across the whole
/// process lifetime, not per-request/per-scope.
/// </summary>
public sealed class NestlyMetricsService : IMetricsService, IDisposable
{
    /// <summary>Meter name the OpenTelemetry SDK is configured to collect from - see AddInfrastructure's <c>WithMetrics</c> call.</summary>
    public const string MeterName = "Nestly";

    private readonly Meter _meter;
    private readonly Counter<long> _paymentOutcomeCounter;
    private readonly Histogram<double> _paymentProcessingDuration;
    private readonly Counter<long> _bookingCreatedCounter;
    private readonly Counter<long> _bookingStatusTransitionCounter;
    private readonly Counter<long> _bookingSlotConflictCounter;
    private readonly Counter<long> _notificationOutcomeCounter;

    private readonly MetricsOptions _options;
    private readonly ILogger<NestlyMetricsService> _logger;
    private readonly FailureRateAlertMonitor _paymentFailureMonitor;
    private readonly FailureRateAlertMonitor _bookingFailureMonitor;
    private readonly ConcurrentDictionary<string, FailureRateAlertMonitor> _notificationFailureMonitors;
    private readonly Func<string, FailureRateAlertMonitor> _createNotificationMonitor;

    public NestlyMetricsService(IOptions<MetricsOptions> options, TimeProvider timeProvider, ILogger<NestlyMetricsService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _notificationFailureMonitors = new ConcurrentDictionary<string, FailureRateAlertMonitor>(StringComparer.OrdinalIgnoreCase);

        var window = TimeSpan.FromMinutes(_options.FailureRateWindowMinutes);
        _paymentFailureMonitor = new FailureRateAlertMonitor(timeProvider, window, _options.PaymentFailureRateThreshold, _options.MinimumSamplesForAlert);
        _bookingFailureMonitor = new FailureRateAlertMonitor(timeProvider, window, _options.BookingFailureRateThreshold, _options.MinimumSamplesForAlert);
        _createNotificationMonitor = _ => new FailureRateAlertMonitor(timeProvider, window, _options.NotificationFailureRateThreshold, _options.MinimumSamplesForAlert);

        _meter = new Meter(MeterName, "1.0.0");

        _paymentOutcomeCounter = _meter.CreateCounter<long>(
            "nestly.payment.outcomes", unit: "{transaction}",
            description: "Payment gateway callback outcomes, tagged by outcome (success/failure).");
        _paymentProcessingDuration = _meter.CreateHistogram<double>(
            "nestly.payment.processing.duration", unit: "ms",
            description: "Payment callback processing latency, tagged by outcome.");
        _bookingCreatedCounter = _meter.CreateCounter<long>(
            "nestly.booking.created", unit: "{booking}",
            description: "Booking creation attempts, tagged by outcome (success/failure).");
        _bookingStatusTransitionCounter = _meter.CreateCounter<long>(
            "nestly.booking.status_transitions", unit: "{transition}",
            description: "Booking lifecycle status transitions, tagged by from/to status.");
        _bookingSlotConflictCounter = _meter.CreateCounter<long>(
            "nestly.booking.slot_conflicts", unit: "{conflict}",
            description: "Booking creation attempts rejected for lack of remaining slot capacity (SRS 12.10.1).");
        _notificationOutcomeCounter = _meter.CreateCounter<long>(
            "nestly.notification.outcomes", unit: "{notification}",
            description: "Notification send attempts, tagged by channel and outcome (success/failure).");
    }

    public void RecordPaymentOutcome(bool succeeded, TimeSpan processingDuration, string? failureReason = null)
    {
        string outcome = succeeded ? "success" : "failure";
        _paymentOutcomeCounter.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        _paymentProcessingDuration.Record(processingDuration.TotalMilliseconds, new KeyValuePair<string, object?>("outcome", outcome));

        double? rate = _paymentFailureMonitor.RecordAndCheck(succeeded);
        if (rate is not null)
        {
            _logger.LogError(
                MetricsAlertEvents.PaymentFailureRateExceeded,
                "ALERT {AlertCode}: payment failure rate is {FailureRatePercent:0.0}% over the trailing {WindowMinutes} minute(s), at or above the {ThresholdPercent:0.0}% alert threshold (latest failure reason: {FailureReason}).",
                "Payment.FailureRateAlert", rate.Value * 100, _options.FailureRateWindowMinutes, _options.PaymentFailureRateThreshold * 100, failureReason ?? "unknown");
        }
    }

    public void RecordBookingCreated(bool succeeded, string? failureReason = null)
    {
        string outcome = succeeded ? "success" : "failure";
        _bookingCreatedCounter.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

        double? rate = _bookingFailureMonitor.RecordAndCheck(succeeded);
        if (rate is not null)
        {
            _logger.LogError(
                MetricsAlertEvents.BookingFailureRateExceeded,
                "ALERT {AlertCode}: booking creation failure rate is {FailureRatePercent:0.0}% over the trailing {WindowMinutes} minute(s), at or above the {ThresholdPercent:0.0}% alert threshold (latest failure reason: {FailureReason}).",
                "Booking.FailureRateAlert", rate.Value * 100, _options.FailureRateWindowMinutes, _options.BookingFailureRateThreshold * 100, failureReason ?? "unknown");
        }
    }

    public void RecordBookingStatusTransition(string fromStatus, string toStatus) =>
        _bookingStatusTransitionCounter.Add(
            1,
            new KeyValuePair<string, object?>("from_status", fromStatus),
            new KeyValuePair<string, object?>("to_status", toStatus));

    public void RecordSlotConflict() => _bookingSlotConflictCounter.Add(1);

    public void RecordNotificationOutcome(string channel, bool succeeded, string? failureReason = null)
    {
        string outcome = succeeded ? "success" : "failure";
        _notificationOutcomeCounter.Add(
            1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("outcome", outcome));

        var monitor = _notificationFailureMonitors.GetOrAdd(channel, _createNotificationMonitor);
        double? rate = monitor.RecordAndCheck(succeeded);
        if (rate is not null)
        {
            _logger.LogError(
                MetricsAlertEvents.NotificationFailureRateExceeded,
                "ALERT {AlertCode}: {Channel} notification failure rate is {FailureRatePercent:0.0}% over the trailing {WindowMinutes} minute(s), at or above the {ThresholdPercent:0.0}% alert threshold (latest failure reason: {FailureReason}).",
                "Notification.FailureRateAlert", channel, rate.Value * 100, _options.FailureRateWindowMinutes, _options.NotificationFailureRateThreshold * 100, failureReason ?? "unknown");
        }
    }

    public void Dispose() => _meter.Dispose();
}
