using Nestly.Application.Referral;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IReferralFraudReviewService"/>.</summary>
public class ReferralFraudReviewService : IReferralFraudReviewService
{
    private readonly IReferralRepository _referralRepository;

    public ReferralFraudReviewService(IReferralRepository referralRepository)
    {
        _referralRepository = referralRepository;
    }

    public async Task<Result> FlagAsync(Guid referralId, Guid? adminUserId, string? note)
    {
        var referral = await _referralRepository.GetByIdAsync(referralId);
        if (referral is null)
        {
            return Result.Failure(Error.NotFound("Referral.NotFound", "This referral does not exist."));
        }

        referral.Flag(adminUserId, note);
        await _referralRepository.UpdateAsync(referral);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(Guid referralId, Guid adminUserId, string? note)
    {
        var referral = await _referralRepository.GetByIdAsync(referralId);
        if (referral is null)
        {
            return Result.Failure(Error.NotFound("Referral.NotFound", "This referral does not exist."));
        }

        if (!referral.IsFraudFlagged)
        {
            return Result.Failure(Error.Business("Referral.NotFlagged", "This referral is not currently flagged for review."));
        }

        referral.Unflag(adminUserId, note is null ? "Confirmed: fraud review approved." : $"Confirmed: {note}");
        await _referralRepository.UpdateAsync(referral);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid referralId, Guid adminUserId, string? note)
    {
        var referral = await _referralRepository.GetByIdAsync(referralId);
        if (referral is null)
        {
            return Result.Failure(Error.NotFound("Referral.NotFound", "This referral does not exist."));
        }

        if (!referral.IsFraudFlagged)
        {
            return Result.Failure(Error.Business("Referral.NotFlagged", "This referral is not currently flagged for review."));
        }

        referral.Unflag(adminUserId, note is null ? "False positive: fraud review rejected." : $"False positive: {note}");
        await _referralRepository.UpdateAsync(referral);
        return Result.Success();
    }
}
