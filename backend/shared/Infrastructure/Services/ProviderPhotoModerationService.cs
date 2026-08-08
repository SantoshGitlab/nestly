using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IProviderPhotoModerationService"/>
public sealed class ProviderPhotoModerationService : IProviderPhotoModerationService
{
    private readonly IProviderRepository _providerRepository;

    public ProviderPhotoModerationService(IProviderRepository providerRepository)
    {
        _providerRepository = providerRepository;
    }

    public async Task<IReadOnlyList<ProviderPhotoResponse>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.ListPendingPhotoModerationAsync(cancellationToken);
        return providers.Select(ProviderDetailMapper.ToPhotoResponse).ToList();
    }

    public Task<Result<ProviderPhotoResponse>> ApproveAsync(Guid providerId, Guid adminUserId, CancellationToken cancellationToken = default) =>
        ApplyVerdictAsync(providerId, provider => provider.ApprovePhoto(adminUserId));

    public Task<Result<ProviderPhotoResponse>> RejectAsync(
        Guid providerId, Guid adminUserId, RejectProviderPhotoRequest request, CancellationToken cancellationToken = default) =>
        ApplyVerdictAsync(providerId, provider => provider.RejectPhoto(adminUserId, request.Reason));

    /// <summary>
    /// The two verdicts differ only in which mutator they call, so the
    /// lookup, the two guards and the write live here once. The guards are
    /// the same pair <see cref="ProviderKycApprovalService"/> applies to a
    /// KYC document: the thing must exist, and it must not already have been
    /// ruled on.
    /// </summary>
    private async Task<Result<ProviderPhotoResponse>> ApplyVerdictAsync(Guid providerId, Action<Provider> applyVerdict)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("ProviderPhotoModeration.ProviderNotFound", "Provider was not found.");
        }

        if (provider.PhotoUrl is null)
        {
            return Error.NotFound("ProviderPhotoModeration.PhotoNotFound", "This provider has not submitted a photo.");
        }

        // Re-approving an already-approved photo, or re-rejecting a rejected
        // one, would rewrite the moderator and timestamp on a decision
        // somebody else already made. A provider who wants another look
        // resubmits, which puts the photo back to Pending.
        if (provider.PhotoModerationStatus != ProviderPhotoModerationStatus.Pending)
        {
            return Error.Business(
                "ProviderPhotoModeration.AlreadyReviewed",
                $"This photo was already {provider.PhotoModerationStatus}.");
        }

        applyVerdict(provider);
        await _providerRepository.UpdateAsync(provider);

        return Result.Success(ProviderDetailMapper.ToPhotoResponse(provider));
    }
}
