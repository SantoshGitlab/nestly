using Nestly.Application.Tracking;

namespace Nestly.Catalog.Tests;

/// <summary>
/// An <see cref="IBookingEtaService"/> that does nothing (task 271), for the
/// tests of other services that merely have to construct a collaborator taking
/// one and have no interest in arrival estimates. The ETA behaviour itself is
/// covered by <c>BookingEtaServiceTests</c> in Identity.Tests, next to the
/// location-ingest tests it hangs off.
/// </summary>
public sealed class NoOpBookingEtaService : IBookingEtaService
{
    public Task RefreshAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ClearAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
