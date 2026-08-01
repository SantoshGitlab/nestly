namespace Nestly.Infrastructure.Observability;

/// <summary>
/// Tracks a rolling, time-windowed failure rate for one metric category
/// (payment outcomes, booking creation, one notification channel) and
/// reports when it crosses an alert threshold (tasks 137a-c, DEVOPS.md
/// OBSERVABILITY "Alerting for critical failures").
///
/// Deliberately not backed by a real time-series store: this is an in-
/// process, best-effort signal for the structured-log alert path, not the
/// metric itself (the Counter/Histogram instruments in
/// <see cref="NestlyMetricsService"/> are the metric; a real monitoring
/// stack, once DEVOPS.md's "Monitoring/alerting stack" open decision is
/// resolved, would compute the same rate off those exported counters and
/// could subsume this). Old samples are trimmed on every write, so memory
/// use is bounded by the event rate over one window, not by process
/// lifetime.
/// </summary>
internal sealed class FailureRateAlertMonitor
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _window;
    private readonly double _failureRateThreshold;
    private readonly int _minimumSamples;
    private readonly Queue<(DateTimeOffset Timestamp, bool Succeeded)> _samples = new();
    private readonly object _gate = new();

    public FailureRateAlertMonitor(TimeProvider timeProvider, TimeSpan window, double failureRateThreshold, int minimumSamples)
    {
        _timeProvider = timeProvider;
        _window = window;
        _failureRateThreshold = failureRateThreshold;
        _minimumSamples = minimumSamples;
    }

    /// <summary>
    /// Records one outcome and evaluates the rolling window. Returns the
    /// current failure rate when this sample leaves the window at or above
    /// the alert threshold (with enough samples to be meaningful);
    /// otherwise null, meaning "no alert this time".
    /// </summary>
    public double? RecordAndCheck(bool succeeded)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            _samples.Enqueue((now, succeeded));

            while (_samples.Count > 0 && now - _samples.Peek().Timestamp > _window)
            {
                _samples.Dequeue();
            }

            if (_samples.Count < _minimumSamples)
            {
                return null;
            }

            int failures = 0;
            foreach (var sample in _samples)
            {
                if (!sample.Succeeded)
                {
                    failures++;
                }
            }

            double rate = (double)failures / _samples.Count;
            return rate >= _failureRateThreshold ? rate : null;
        }
    }
}
