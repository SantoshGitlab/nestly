using Nestly.Application.Escrow;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Platform escrow hold/release bookkeeping (task 158). A single
/// platform-owned running balance, mirroring <see cref="WalletService"/>'s
/// "no separate mutable balance column" design - every entry both records
/// what happened and snapshots the resulting balance.
/// </summary>
public class EscrowService : IEscrowService
{
    private readonly IPlatformEscrowLedgerRepository _repository;

    public EscrowService(IPlatformEscrowLedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HoldAsync(Guid bookingId, Guid paymentTransactionId, decimal amount)
    {
        decimal currentBalance = (await _repository.GetLatestAsync())?.BalanceAfter ?? 0m;
        var entry = new PlatformEscrowLedger(
            Guid.NewGuid(), bookingId, EscrowEntryType.Hold, amount, currentBalance + amount,
            EscrowSourceType.PaymentConfirmed, paymentTransactionId,
            "Payment confirmed - funds held in platform escrow pending fulfilment.");

        await _repository.AddAsync(entry);
    }

    public async Task<EscrowReleaseResult?> ReleaseToProviderAsync(Guid bookingId, Guid paymentTransactionId, Guid? providerId, decimal commissionAmount)
    {
        decimal held = await GetHeldBalanceAsync(bookingId);
        if (held <= 0)
        {
            // Already released (to a provider or via refund), or never held.
            return null;
        }

        if (commissionAmount < 0 || commissionAmount > held)
        {
            throw new ArgumentOutOfRangeException(nameof(commissionAmount), "Commission amount must be between 0 and the booking's held escrow balance.");
        }

        decimal currentBalance = (await _repository.GetLatestAsync())?.BalanceAfter ?? 0m;
        var entry = new PlatformEscrowLedger(
            Guid.NewGuid(), bookingId, EscrowEntryType.Release, held, currentBalance - held,
            EscrowSourceType.BookingCompleted, paymentTransactionId,
            "Booking completed - escrow released to provider net of commission.",
            providerId, commissionAmount);

        await _repository.AddAsync(entry);

        return new EscrowReleaseResult(held, commissionAmount, held - commissionAmount);
    }

    public async Task ReleaseForRefundAsync(Guid bookingId, Guid refundTransactionId, decimal refundAmount)
    {
        decimal held = await GetHeldBalanceAsync(bookingId);
        if (held <= 0)
        {
            // Nothing left in escrow for this booking - it was already
            // released to its provider before this refund was issued.
            return;
        }

        decimal releaseAmount = Math.Min(held, refundAmount);
        decimal currentBalance = (await _repository.GetLatestAsync())?.BalanceAfter ?? 0m;
        var entry = new PlatformEscrowLedger(
            Guid.NewGuid(), bookingId, EscrowEntryType.Release, releaseAmount, currentBalance - releaseAmount,
            EscrowSourceType.RefundIssued, refundTransactionId,
            "Refund issued - escrow released back out (not paid to a provider).");

        await _repository.AddAsync(entry);
    }

    public async Task<decimal> GetHeldBalanceAsync(Guid bookingId)
    {
        var entries = await _repository.ListByBookingAsync(bookingId);
        decimal held = 0m;
        foreach (var entry in entries)
        {
            held += entry.EntryType == EscrowEntryType.Hold ? entry.Amount : -entry.Amount;
        }

        return held;
    }
}
