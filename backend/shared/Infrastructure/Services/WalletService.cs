using Nestly.Application.Wallet;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Wallet balance and append-only ledger (SRS 11.17, 14.5, tasks 74a-c). The
/// current balance is never stored on its own - it is always the latest
/// ledger entry's <see cref="WalletLedgerEntry.BalanceAfter"/> (or zero, with
/// no activity), which is what "append-only or traceable" (SRS 14.5) means
/// in practice: every entry is a self-contained audit record of the balance
/// at that moment, not just a delta.
/// </summary>
public class WalletService : IWalletService
{
    private readonly IWalletLedgerRepository _repository;

    public WalletService(IWalletLedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<WalletBalanceResponse>> GetBalanceAsync(Guid customerId)
    {
        var latest = await _repository.GetLatestAsync(customerId);
        return Result.Success(new WalletBalanceResponse(latest?.BalanceAfter ?? 0m));
    }

    public async Task<Result<IReadOnlyList<WalletLedgerEntryResponse>>> GetLedgerAsync(Guid customerId)
    {
        var entries = await _repository.ListByCustomerAsync(customerId);
        IReadOnlyList<WalletLedgerEntryResponse> response = entries
            .Select(e => new WalletLedgerEntryResponse(e.Id, e.EntryType, e.Amount, e.BalanceAfter, e.SourceType, e.SourceReferenceId, e.Description, e.CreatedAtUtc, e.ExpiresAtUtc))
            .ToList();

        return Result.Success(response);
    }

    public async Task<WalletLedgerEntry> CreditAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description, DateTime? expiresAtUtc = null)
    {
        decimal currentBalance = (await _repository.GetLatestAsync(customerId))?.BalanceAfter ?? 0m;
        var entry = new WalletLedgerEntry(
            Guid.NewGuid(), customerId, WalletEntryType.Credit, amount, currentBalance + amount, sourceType, sourceReferenceId, description, expiresAtUtc);

        await _repository.AddAsync(entry);
        return entry;
    }

    public async Task<Result<WalletLedgerEntry>> DebitAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description)
    {
        decimal currentBalance = (await _repository.GetLatestAsync(customerId))?.BalanceAfter ?? 0m;
        if (amount > currentBalance)
        {
            return Error.Business("Wallet.InsufficientBalance", "The wallet does not have enough balance for this debit.");
        }

        var entry = new WalletLedgerEntry(
            Guid.NewGuid(), customerId, WalletEntryType.Debit, amount, currentBalance - amount, sourceType, sourceReferenceId, description);

        await _repository.AddAsync(entry);
        await ConsumeExpiringCreditsAsync(customerId, amount);
        return entry;
    }

    public async Task<WalletLedgerEntry?> ExpireCreditAsync(Guid customerId, Guid expiredEntryId, decimal amount)
    {
        if (amount <= 0)
        {
            return null;
        }

        decimal currentBalance = (await _repository.GetLatestAsync(customerId))?.BalanceAfter ?? 0m;

        // Should never happen given the FIFO invariant (a customer's balance
        // can never fall below the sum of their still-outstanding expiring
        // credits' RemainingAmount, since every debit consumes both together)
        // - defensive clamp rather than throwing, so one inconsistent row
        // never breaks the whole sweep run for every other customer.
        decimal writeOffAmount = Math.Min(amount, currentBalance);
        if (writeOffAmount <= 0)
        {
            return null;
        }

        var entry = new WalletLedgerEntry(
            Guid.NewGuid(), customerId, WalletEntryType.Debit, writeOffAmount, currentBalance - writeOffAmount,
            WalletSourceType.ReferralCreditExpiry, expiredEntryId, "Referral credit expired");

        await _repository.AddAsync(entry);
        return entry;
    }

    /// <summary>Task 175's FIFO consumption: a debit draws from the customer's soonest-to-expire outstanding credits first, before falling back to non-expiring balance.</summary>
    private async Task ConsumeExpiringCreditsAsync(Guid customerId, decimal debitAmount)
    {
        decimal remaining = debitAmount;
        var expiringCredits = await _repository.ListUnexpiredCreditsWithRemainingAsync(customerId, DateTime.UtcNow);

        foreach (var credit in expiringCredits)
        {
            if (remaining <= 0)
            {
                break;
            }

            decimal consume = Math.Min(remaining, credit.RemainingAmount!.Value);
            credit.ConsumeRemaining(consume);
            await _repository.UpdateRemainingAsync(credit);
            remaining -= consume;
        }
    }
}
