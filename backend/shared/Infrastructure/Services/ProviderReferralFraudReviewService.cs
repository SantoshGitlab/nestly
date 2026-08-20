using Nestly.Application.ProviderReferral;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderReferralFraudReviewService"/>. Mirrors ReferralFraudReviewService.</summary>
public class ProviderReferralFraudReviewService : IProviderReferralFraudReviewService
{
    private readonly IProviderReferralRepository _referralRepository;

    public ProviderReferralFraudReviewService(IProviderReferralRepository referralRepository)
    {
        _referralRepository = referralRepository;
    }

    public async Task<Result> FlagAsync(Guid referralId, Guid? adminUserId, string? note)
    {
        var referral = await _referralRepository.GetByIdAsync(referralId);
        if (referral is null)
        {
            return Result.Failure(Error.NotFound("ProviderReferral.NotFound", "This provider referral does not exist."));
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
            return Result.Failure(Error.NotFound("ProviderReferral.NotFound", "This provider referral does not exist."));
        }

        if (!referral.IsFraudFlagged)
        {
            return Result.Failure(Error.Business("ProviderReferral.NotFlagged", "This provider referral is not currently flagged for review."));
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
            return Result.Failure(Error.NotFound("ProviderReferral.NotFound", "This provider referral does not exist."));
        }

        if (!referral.IsFraudFlagged)
        {
            return Result.Failure(Error.Business("ProviderReferral.NotFlagged", "This provider referral is not currently flagged for review."));
        }

        referral.Unflag(adminUserId, note is null ? "False positive: fraud review rejected." : $"False positive: {note}");
        await _referralRepository.UpdateAsync(referral);
        return Result.Success();
    }
}
