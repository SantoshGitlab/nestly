namespace Nestly.Application.Escrow;

/// <summary>The outcome of releasing a booking's held escrow to its provider.</summary>
public record EscrowReleaseResult(decimal GrossAmount, decimal CommissionAmount, decimal NetAmountToProvider);

/// <summary>
/// The platform-owned escrow holding account (task 158): a customer's
/// payment is held here from confirmation until the booking is either
/// completed (released to the provider net of commission) or refunded
/// (released back out, un-paid). Mirrors the append-only bookkeeping
/// <see cref="Nestly.Application.Wallet.IWalletService"/> already uses for
/// the customer wallet - this is not a payout system, just the hold/release
/// ledger entries.
/// </summary>
public interface IEscrowService
{
    /// <summary>
    /// Moves a just-confirmed payment's amount into escrow (task 158).
    /// Called once per booking, right after its payment succeeds - reaching
    /// this twice for the same payment is already prevented upstream by the
    /// webhook handler's own attempt-status idempotency guard.
    /// </summary>
    Task HoldAsync(Guid bookingId, Guid paymentTransactionId, decimal amount);

    /// <summary>
    /// Releases a booking's currently-held escrow to its provider, net of
    /// <paramref name="commissionAmount"/> (task 157's already-recorded
    /// figure, reused here rather than recomputed, so the settlement and the
    /// escrow ledger never disagree). <paramref name="providerId"/> is a
    /// placeholder (task 158) - there is no Provider identity in the domain
    /// yet, so it may be null. Returns null (a no-op) if nothing is
    /// currently held for the booking - already released, or never held.
    /// </summary>
    Task<EscrowReleaseResult?> ReleaseToProviderAsync(Guid bookingId, Guid paymentTransactionId, Guid? providerId, decimal commissionAmount);

    /// <summary>
    /// Releases up to <paramref name="refundAmount"/> of a booking's
    /// currently-held escrow back out, because a refund was issued for it
    /// (task 158's refund-path handling) - the refund's actual money
    /// movement is unchanged (still <see cref="Nestly.Application.Wallet.IWalletService"/>
    /// or the gateway); this only keeps the escrow bookkeeping honest. A
    /// no-op if nothing remains held (e.g. the booking was already released
    /// to its provider on completion before this refund was issued).
    /// </summary>
    Task ReleaseForRefundAsync(Guid bookingId, Guid refundTransactionId, decimal refundAmount);

    /// <summary>The sum of a booking's Hold entries minus its Release entries - what remains held right now.</summary>
    Task<decimal> GetHeldBalanceAsync(Guid bookingId);
}
