namespace Nestly.Application.Abstractions.Observability;

/// <summary>
/// Application-facing seam for recording operational metrics (SRS 29.6, tasks
/// 137a-c; DEVOPS.md OBSERVABILITY). Business services depend on this small,
/// intention-revealing interface rather than on <c>System.Diagnostics.Metrics</c>
/// directly, the same reason <c>IAuditLogWriter</c> and <c>ICacheService</c>
/// exist - Infrastructure decides how a recorded fact becomes an exported
/// counter/histogram (and, for the failure-rate methods below, an alert-worthy
/// structured log), Application/business code just reports the fact.
///
/// DEVOPS.md leaves the monitoring/alerting stack as an open decision (no
/// Slack/PagerDuty/email destination is wired up yet), so "alert" here means a
/// distinctly tagged, error-level structured log event (see
/// <c>Nestly.Infrastructure.Observability.MetricsAlertEvents</c>) that an
/// external alert rule (Serilog sink, log-based alerting in whatever
/// monitoring stack DEVOPS.md eventually settles on) can match on - not a
/// fabricated webhook integration.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records the terminal outcome of a payment gateway callback (task 137a):
    /// one payment success/failure counter increment plus one processing-
    /// latency histogram observation, both tagged by outcome. Also feeds the
    /// rolling payment failure-rate alert - see the interface doc comment.
    /// </summary>
    /// <param name="succeeded">Whether the payment attempt succeeded.</param>
    /// <param name="processingDuration">Time taken to process the callback end to end.</param>
    /// <param name="failureReason">Gateway-reported failure reason; ignored when <paramref name="succeeded"/> is true.</param>
    void RecordPaymentOutcome(bool succeeded, TimeSpan processingDuration, string? failureReason = null);

    /// <summary>
    /// Records the outcome of a single booking-creation attempt (task 137b).
    /// Also feeds the rolling booking failure-rate alert.
    /// </summary>
    /// <param name="succeeded">Whether the booking was created.</param>
    /// <param name="failureReason">Business/error code describing why creation was rejected; ignored when <paramref name="succeeded"/> is true.</param>
    void RecordBookingCreated(bool succeeded, string? failureReason = null);

    /// <summary>
    /// Records a booking moving from one lifecycle status to another (task
    /// 137b) - every <see cref="Nestly.Domain.Events.BookingStatusChangedEvent"/>
    /// the platform raises, not just the creation-time transition, so this
    /// also captures cancellations, reschedules, refunds, and completions.
    /// </summary>
    void RecordBookingStatusTransition(string fromStatus, string toStatus);

    /// <summary>
    /// Records a booking-creation attempt rejected because the requested slot
    /// had no remaining per-day capacity (task 137b, SRS 12.10.1). Kept as its
    /// own counter (rather than folded into <see cref="RecordBookingCreated"/>'s
    /// failure reason) so "slot-conflict rate" can be graphed directly as
    /// this counter's rate against <see cref="RecordBookingCreated"/>'s total,
    /// without parsing failure-reason label values.
    /// </summary>
    void RecordSlotConflict();

    /// <summary>
    /// Records the outcome of a single-channel notification send attempt
    /// (task 137c) - SMS, email, or push are each their own channel value.
    /// Also feeds a per-channel rolling failure-rate alert, so an outage in
    /// one provider (e.g. the SMS gateway) is not masked by the other
    /// channels' healthy send rates.
    /// </summary>
    /// <param name="channel">Channel name, e.g. "Sms", "Email", "Push".</param>
    /// <param name="succeeded">Whether the send attempt succeeded.</param>
    /// <param name="failureReason">Provider/error reason; ignored when <paramref name="succeeded"/> is true.</param>
    void RecordNotificationOutcome(string channel, bool succeeded, string? failureReason = null);
}
