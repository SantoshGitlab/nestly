using System.Runtime.CompilerServices;
using Nestly.Application.ProviderManagement;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Always reports exactly one eligible provider. A stand-in for real provider
/// matching in suites that build <c>PaymentService</c> only to push a booking
/// through to Confirmed as setup for something else they're actually testing
/// (refunds, cancellations, commission/escrow, notifications, and so on) - not
/// to exercise <c>CreateOrderAsync</c>'s Payment.NoProviderAvailable gate (see
/// PaymentServiceTests for that coverage). Seeding a fully-eligible Provider
/// (skill mapping, service area, availability window) in every one of those
/// fixtures would be unrelated setup for a check none of them mean to cover.
/// </summary>
public sealed class AlwaysEligibleProviderSearchStub : IEligibleProviderSearchService
{
    public async IAsyncEnumerable<ProviderMatchCandidate> FindEligibleAsync(
        Guid bookingId,
        IReadOnlyCollection<Guid>? excludeProviderIds = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ProviderMatchCandidate(Guid.NewGuid(), null);
    }
}
