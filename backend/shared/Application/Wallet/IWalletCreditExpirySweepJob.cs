namespace Nestly.Application.Wallet;

/// <summary>
/// Task 175's scheduled sweep: auto-debits the unconsumed portion of any
/// wallet credit past its <see cref="Nestly.Domain.WalletLedgerEntry.ExpiresAtUtc"/>.
/// Registered as a Hangfire recurring job (see <c>BackgroundJobRegistration</c>
/// / admin-api <c>Program.cs</c>) - the interface lives in Application so the
/// job can be resolved through DI/Hangfire's activator the same way
/// <c>IExportJobService</c> is.
/// </summary>
public interface IWalletCreditExpirySweepJob
{
    /// <summary>Idempotent and safe to re-run (Hangfire's retry convention requires this) - an already-expired-and-written-off entry has RemainingAmount 0 and is simply not picked up again.</summary>
    Task SweepAsync(CancellationToken cancellationToken = default);
}
