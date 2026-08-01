using Microsoft.Extensions.Logging;

namespace Nestly.Infrastructure.Observability;

/// <summary>
/// Distinct <see cref="EventId"/>s for the alert-worthy structured log events
/// <see cref="NestlyMetricsService"/> raises (tasks 137a-c; DEVOPS.md
/// OBSERVABILITY "Alerting for critical failures - payment, booking, and
/// notification failures at minimum"). No alerting destination (Slack/
/// PagerDuty/email) is decided yet in DEVOPS.md's OPEN DECISIONS, so the
/// correct scope here is a well-tagged, error-level structured log an
/// external alert rule can match on - by <see cref="EventId.Id"/>,
/// <see cref="EventId.Name"/>, or the "AlertCode" property every one of
/// these events also carries in its message template - rather than a
/// fabricated webhook integration.
/// </summary>
public static class MetricsAlertEvents
{
    public static readonly EventId PaymentFailureRateExceeded = new(90001, "PaymentFailureRateExceeded");

    public static readonly EventId BookingFailureRateExceeded = new(90002, "BookingFailureRateExceeded");

    public static readonly EventId NotificationFailureRateExceeded = new(90003, "NotificationFailureRateExceeded");
}
