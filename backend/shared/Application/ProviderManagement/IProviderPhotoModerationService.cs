using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Admin moderation of provider-supplied profile photos (task 293).
///
/// A profile photo is user-supplied content shown to customers, and this
/// codebase's existing standard for exactly that is a pending/approved/
/// rejected gate an admin drives: <see cref="IProviderKycApprovalService"/>
/// for KYC documents, <c>IReviewModerationService</c> for review text. A
/// photo goes through the same gate rather than publishing on upload.
///
/// Kept separate from <see cref="IProviderManagementService"/> for the same
/// reason KYC approval is: this is an approval workflow with its own verdict
/// and its own queue, not field-level CRUD on a provider record.
/// </summary>
public interface IProviderPhotoModerationService
{
    /// <summary>Every provider whose photo is awaiting a verdict - the moderation queue itself, oldest submission first so nothing starves.</summary>
    Task<IReadOnlyList<ProviderPhotoResponse>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Approves the photo, which is the only thing that makes it visible to customers.</summary>
    Task<Result<ProviderPhotoResponse>> ApproveAsync(Guid providerId, Guid adminUserId, CancellationToken cancellationToken = default);

    /// <summary>Rejects the photo. The image is kept, not wiped - the provider needs to see what was rejected and the reason has to be attributable to it.</summary>
    Task<Result<ProviderPhotoResponse>> RejectAsync(Guid providerId, Guid adminUserId, RejectProviderPhotoRequest request, CancellationToken cancellationToken = default);
}
