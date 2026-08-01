using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Metrics" configuration section (SRS 29.6,
/// tasks 137a-c; DEVOPS.md OBSERVABILITY "Alerting for critical failures -
/// payment, booking, and notification failures at minimum").
///
/// Governs the rolling failure-rate window <see cref="NestlyMetricsService"/>
/// uses to decide when a payment/booking/notification failure rate is
/// alert-worthy. Not a secret and has safe production-sensible defaults
/// (same reasoning as <see cref="CommissionOptions"/>), so an environment
/// that says nothing still gets working alerting rather than failing to
/// start.
/// </summary>
public class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>Rolling window, in minutes, used to compute failure rates for alerting.</summary>
    [Range(1, 1440)]
    public double FailureRateWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Minimum number of samples inside the window before a computed rate is
    /// considered meaningful enough to alert on - guards against a single
    /// failure out of one or two attempts reading as a "100% failure rate".
    /// </summary>
    [Range(1, 10_000)]
    public int MinimumSamplesForAlert { get; set; } = 10;

    /// <summary>Payment outcome failure rate (0-1) that triggers an alert-worthy log event.</summary>
    [Range(0.0, 1.0)]
    public double PaymentFailureRateThreshold { get; set; } = 0.2;

    /// <summary>Booking-creation failure rate (0-1) that triggers an alert-worthy log event.</summary>
    [Range(0.0, 1.0)]
    public double BookingFailureRateThreshold { get; set; } = 0.2;

    /// <summary>Per-channel notification send failure rate (0-1) that triggers an alert-worthy log event.</summary>
    [Range(0.0, 1.0)]
    public double NotificationFailureRateThreshold { get; set; } = 0.2;
}
