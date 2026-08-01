using Nestly.Application.Abstractions.Observability;

namespace Nestly.Catalog.Tests;

/// <summary>
/// No-op <see cref="IMetricsService"/> for tests that construct services
/// directly (no DI container) and don't assert on metrics themselves -
/// exercising the real <c>NestlyMetricsService</c> here would pull in
/// Microsoft.Extensions.Logging/Options wiring these tests have no other
/// reason to set up, for a signal none of them check.
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
