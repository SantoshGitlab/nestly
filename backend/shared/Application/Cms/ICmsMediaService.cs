using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cms;

/// <summary>Admin CRUD over the CMS media library (SRS 12.16.2, task 124e). Referenced by <c>IBannerService</c> when a banner picks an asset - see <see cref="Banner"/>'s doc comment.</summary>
public interface ICmsMediaService
{
    Task<IReadOnlyList<CmsMediaResponse>> ListAsync();

    Task<Result<CmsMediaResponse>> GetByIdAsync(Guid id);

    Task<Result<CmsMediaResponse>> CreateAsync(CmsMediaCreateRequest request);

    Task<Result<CmsMediaResponse>> UpdateAsync(Guid id, CmsMediaUpdateRequest request);

    /// <summary>Fails with a conflict if any banner still references this asset (see <c>ICmsMediaRepository.IsReferencedByBannerAsync</c>).</summary>
    Task<Result> DeleteAsync(Guid id);
}
