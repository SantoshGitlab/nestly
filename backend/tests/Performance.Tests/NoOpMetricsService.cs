using Nestly.Application.Abstractions.Observability;

namespace Nestly.Performance.Tests;

/// <summary>
/// No-op <see cref="IMetricsService"/> for perf tests that construct services
/// directly (no DI container) - see Catalog.Tests' NoOpMetricsService for the
/// same reasoning; kept as a separate copy per that project's own
/// InMemoryCacheService precedent rather than a shared reference between test
/// assemblies.
/// </summary>
public sealed class NoOpMetricsService : IMetricsService
{
    public void RecordPaymentOutcome(bool succeeded, TimeSpan processingDuration, string? failureReason = null)
    {
    }

    public void RecordBookingCreated(bool succeeded, string? failureReason = null)
    {
    }

    public void RecordBookingStatusTransition(string fromStatus, string toStatus)
    {
    }

    public void RecordSlotConflict()
    {
    }

    public void RecordNotificationOutcome(string channel, bool succeeded, string? failureReason = null)
    {
    }
}
