using Microsoft.Extensions.Logging;
using Nestly.Application.Wallet;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IWalletCreditExpirySweepJob"/>.</summary>
public class WalletCreditExpirySweepJob : IWalletCreditExpirySweepJob
{
    private readonly IWalletLedgerRepository _walletLedgerRepository;
    private readonly IWalletService _walletService;
    private readonly ILogger<WalletCreditExpirySweepJob> _logger;

    public WalletCreditExpirySweepJob(
        IWalletLedgerRepository walletLedgerRepository,
        IWalletService walletService,
        ILogger<WalletCreditExpirySweepJob> logger)
    {
        _walletLedgerRepository = walletLedgerRepository;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var expired = await _walletLedgerRepository.ListExpiredCreditsWithRemainingAsync(nowUtc);

        int swept = 0;
        foreach (var credit in expired)
        {
            cancellationToken.ThrowIfCancellationRequested();

            decimal amount = credit.RemainingAmount!.Value;
            var writeOff = await _walletService.ExpireCreditAsync(credit.CustomerId, credit.Id, amount);

            // Zero out the source entry's tracked remaining amount regardless
            // of whether a write-off entry was actually created (the
            // defensive clamp in ExpireCreditAsync can skip it e.g. if the
            // balance is already 0) - either way this credit's expiry window
            // has closed and it must never be picked up by the sweep again.
            credit.ExpireRemaining();
            await _walletLedgerRepository.UpdateRemainingAsync(credit);

            if (writeOff is not null)
            {
                swept++;
            }
        }

        _logger.LogInformation(
            "Wallet credit expiry sweep: {ExpiredCount} expiring credit(s) found, {WrittenOffCount} written off.",
            expired.Count, swept);
    }
}
