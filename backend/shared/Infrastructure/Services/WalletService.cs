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
            .Select(e => new WalletLedgerEntryResponse(e.Id, e.EntryType, e.Amount, e.BalanceAfter, e.SourceType, e.SourceReferenceId, e.Description, e.CreatedAtUtc))
            .ToList();

        return Result.Success(response);
    }

    public async Task<WalletLedgerEntry> CreditAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description)
    {
        decimal currentBalance = (await _repository.GetLatestAsync(customerId))?.BalanceAfter ?? 0m;
        var entry = new WalletLedgerEntry(
            Guid.NewGuid(), customerId, WalletEntryType.Credit, amount, currentBalance + amount, sourceType, sourceReferenceId, description);

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
        return entry;
    }
}
