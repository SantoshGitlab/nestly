using Nestly.Domain;

namespace Nestly.Application.Escrow;

public interface IPlatformEscrowLedgerRepository
{
    /// <summary>Entries are append-only - there is deliberately no Update/Delete method (task 158, mirrors SRS 14.5's wallet rule).</summary>
    Task AddAsync(PlatformEscrowLedger entry);

    /// <summary>The most recent entry across the whole platform, whose BalanceAfter is the platform's current escrow balance (null when there has never been any escrow activity).</summary>
    Task<PlatformEscrowLedger?> GetLatestAsync();

    /// <summary>Every entry recorded against one booking, oldest first - used to derive that booking's currently-held (un-released) balance.</summary>
    Task<IReadOnlyList<PlatformEscrowLedger>> ListByBookingAsync(Guid bookingId);
}
