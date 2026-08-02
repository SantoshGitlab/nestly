using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.NestlyCoins;
using Nestly.Application.PartnerManagement;
using Nestly.Application.Wallet;
using Nestly.Domain;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="INestlyCoinsService"/>
public class NestlyCoinsService : INestlyCoinsService
{
    private readonly INestlyCoinsProgramConfigRepository _configRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IWalletService _walletService;
    private readonly IWalletLedgerRepository _walletLedgerRepository;
    private readonly IPartnerEarningLedgerService _partnerEarningLedgerService;
    private readonly IPartnerEarningLedgerRepository _partnerEarningLedgerRepository;
    private readonly ILogger<NestlyCoinsService> _logger;

    public NestlyCoinsService(
        INestlyCoinsProgramConfigRepository configRepository,
        IBookingRepository bookingRepository,
        IWalletService walletService,
        IWalletLedgerRepository walletLedgerRepository,
        IPartnerEarningLedgerService partnerEarningLedgerService,
        IPartnerEarningLedgerRepository partnerEarningLedgerRepository,
        ILogger<NestlyCoinsService> logger)
    {
        _configRepository = configRepository;
        _bookingRepository = bookingRepository;
        _walletService = walletService;
        _walletLedgerRepository = walletLedgerRepository;
        _partnerEarningLedgerService = partnerEarningLedgerService;
        _partnerEarningLedgerRepository = partnerEarningLedgerRepository;
        _logger = logger;
    }

    public bool EvaluateQualifyingOrder(NestlyCoinsProgramConfig config, decimal orderAmount, int priorCompletedCount, decimal creditedThisMonth)
    {
        if (!config.IsActive)
        {
            return false;
        }

        if (orderAmount < config.MinimumOrderAmount)
        {
            return false;
        }

        if (config.RequireReorder && priorCompletedCount == 0)
        {
            return false;
        }

        decimal earned = CalculateEarnAmount(config, orderAmount);
        if (config.MaxCoinsPerMonth is decimal cap && creditedThisMonth + earned > cap)
        {
            return false;
        }

        return earned > 0;
    }

    public async Task CreditCustomerCoinsAsync(Booking booking)
    {
        var config = await _configRepository.GetByAudienceAsync(NestlyCoinsAudience.Customer);
        if (config is null)
        {
            return;
        }

        int priorCompleted = await _bookingRepository.CountCompletedByCustomerAsync(booking.CustomerId, booking.Id);
        decimal creditedThisMonth = await _walletLedgerRepository.SumCreditsBySourceTypeInRangeAsync(
            booking.CustomerId, WalletSourceType.NestlyCoinsReward, CurrentMonthStartUtc(), NextMonthStartUtc());

        if (!EvaluateQualifyingOrder(config, booking.TotalPayableSnapshot, priorCompleted, creditedThisMonth))
        {
            return;
        }

        decimal amount = CalculateEarnAmount(config, booking.TotalPayableSnapshot);

        // Coins always carry an expiry (GUIDELINES #3) - this is what makes
        // WalletService.CreditAsync's FIFO consumption tracking (task 175,
        // confirmed working against main as part of task 199's resolution)
        // apply to these credits.
        await _walletService.CreditAsync(
            booking.CustomerId, amount, WalletSourceType.NestlyCoinsReward, booking.Id,
            $"Nestly Coins earned - booking {booking.Id}.",
            expiresAtUtc: DateTime.UtcNow.AddDays(config.ExpiryDays));
    }

    public async Task CreditPartnerCoinsAsync(Booking booking)
    {
        if (booking.AssignedPartnerId is not Guid partnerId)
        {
            return;
        }

        var config = await _configRepository.GetByAudienceAsync(NestlyCoinsAudience.Partner);
        if (config is null)
        {
            return;
        }

        int priorCompleted = await _bookingRepository.CountCompletedByAssignedPartnerAsync(partnerId, booking.Id);
        decimal creditedThisMonth = await _partnerEarningLedgerRepository.SumCreditsBySourceTypeInRangeAsync(
            partnerId, PartnerEarningSourceType.NestlyCoinsReward, CurrentMonthStartUtc(), NextMonthStartUtc());

        if (!EvaluateQualifyingOrder(config, booking.TotalPayableSnapshot, priorCompleted, creditedThisMonth))
        {
            return;
        }

        decimal amount = CalculateEarnAmount(config, booking.TotalPayableSnapshot);

        // Unlike WalletLedgerEntry, PartnerEarningLedgerEntry has no
        // ExpiresAtUtc/RemainingAmount - the partner earning ledger settles
        // via periodic PartnerPayout batches rather than per-item spend-down,
        // so there is no equivalent per-entry expiry to set here. This is a
        // real architectural asymmetry, not an oversight: GUIDELINES #3's
        // FIFO-expiry prerequisite is specifically about WalletLedgerEntry.
        var result = await _partnerEarningLedgerService.RecordAdjustmentAsync(
            partnerId,
            new RecordPartnerEarningAdjustmentRequest(
                PartnerEarningEntryType.Credit,
                amount,
                PartnerEarningSourceType.NestlyCoinsReward,
                booking.Id,
                $"Nestly Coins earned - booking {booking.Id}."));

        if (result.IsFailure)
        {
            // Fire-and-forget domain event handler, not inside the caller's
            // own unit of work (same reasoning EscrowReleaseOnCompletionHandler
            // already established for this exact call) - logged for admin
            // reconciliation rather than thrown.
            _logger.LogWarning(
                "Failed to credit Nestly Coins to partner {PartnerId} for booking {BookingId}: {ErrorCode} {ErrorMessage}",
                partnerId, booking.Id, result.Error.Code, result.Error.Message);
        }
    }

    public async Task ClawbackOnCancellationAsync(Guid bookingId)
    {
        await ClawbackCustomerCreditAsync(bookingId);
        await ClawbackPartnerCreditAsync(bookingId);
    }

    private async Task ClawbackCustomerCreditAsync(Guid bookingId)
    {
        var credit = await _walletLedgerRepository.FindBySourceAsync(WalletSourceType.NestlyCoinsReward, bookingId);
        if (credit is null)
        {
            return;
        }

        var config = await _configRepository.GetByAudienceAsync(NestlyCoinsAudience.Customer);
        if (config is null || DateTime.UtcNow > credit.CreatedAtUtc.AddDays(config.ClawbackWindowDays))
        {
            return;
        }

        // Reverse only the still-unspent portion of THIS credit
        // (RemainingAmount, task 175's FIFO tracking) rather than the full
        // original Amount - a customer who already spent part of it
        // elsewhere must not have unrelated balance clawed back too.
        decimal amountToReverse = credit.RemainingAmount ?? credit.Amount;
        if (amountToReverse <= 0)
        {
            return;
        }

        var debitResult = await _walletService.DebitAsync(
            credit.CustomerId, amountToReverse, WalletSourceType.NestlyCoinsClawback, bookingId,
            $"Nestly Coins clawed back - booking {bookingId} cancelled within the clawback window.");

        if (debitResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to claw back Nestly Coins from customer {CustomerId} for cancelled booking {BookingId}: {ErrorCode} {ErrorMessage}",
                credit.CustomerId, bookingId, debitResult.Error.Code, debitResult.Error.Message);
        }
    }

    private async Task ClawbackPartnerCreditAsync(Guid bookingId)
    {
        var credit = await _partnerEarningLedgerRepository.FindBySourceAsync(PartnerEarningSourceType.NestlyCoinsReward, bookingId);
        if (credit is null)
        {
            return;
        }

        var config = await _configRepository.GetByAudienceAsync(NestlyCoinsAudience.Partner);
        if (config is null || DateTime.UtcNow > credit.CreatedAtUtc.AddDays(config.ClawbackWindowDays))
        {
            return;
        }

        // No RemainingAmount concept on PartnerEarningLedgerEntry (see
        // CreditPartnerCoinsAsync's comment) - the full originally credited
        // amount is reversed, since there is no per-entry consumption
        // tracking to draw a partial figure from.
        var debitResult = await _partnerEarningLedgerService.RecordAdjustmentAsync(
            credit.PartnerId,
            new RecordPartnerEarningAdjustmentRequest(
                PartnerEarningEntryType.Debit,
                credit.Amount,
                PartnerEarningSourceType.NestlyCoinsClawback,
                bookingId,
                $"Nestly Coins clawed back - booking {bookingId} cancelled within the clawback window."));

        if (debitResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to claw back Nestly Coins from partner {PartnerId} for cancelled booking {BookingId}: {ErrorCode} {ErrorMessage}",
                credit.PartnerId, bookingId, debitResult.Error.Code, debitResult.Error.Message);
        }
    }

    private static decimal CalculateEarnAmount(NestlyCoinsProgramConfig config, decimal orderAmount) =>
        Math.Round(orderAmount / 100m * config.EarnRatePer100, 2, MidpointRounding.AwayFromZero);

    private static DateTime CurrentMonthStartUtc()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime NextMonthStartUtc() => CurrentMonthStartUtc().AddMonths(1);
}
