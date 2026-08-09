using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cms;

/// <summary>Admin CRUD over the CMS media library (SRS 12.16.2, task 124e). Referenced by <c>IBannerService</c> when a banner picks an asset - see <see cref="Banner"/>'s doc comment.</summary>
public interface ICmsMediaService
{
    Task<IReadOnlyList<CmsMediaResponse>> ListAsync();

    Task<Result<CmsMediaResponse>> GetByIdAsync(Guid id);

    Task<Result<CmsMediaResponse>> CreateAsync(CmsMediaCreateRequest request);

    /// <summary>
    /// Task 314: stores <paramref name="content"/> via <c>IFileStorageService</c>
    /// and returns the origin-relative ref it was saved under (e.g.
    /// "/uploads/&lt;guid&gt;.jpg") - not yet a <see cref="CmsMedia"/> row.
    /// <c>IFileStorageService</c> knows nothing about HTTP, so it cannot
    /// resolve an absolute URL; the caller (the controller, which has the
    /// request's scheme/host) does that and then calls <see cref="CreateAsync"/>
    /// with the result, same two-step shape as provider-api's completion-photo
    /// upload. A relative ref stored as-is would break the moment the asset
    /// is read from a different origin than the one that uploaded it (every
    /// other consumer of this library - customer-web, admin-web itself).
    /// </summary>
    Task<string> SaveFileAsync(Stream content, string fileNameHint, string contentType, CancellationToken cancellationToken = default);

    Task<Result<CmsMediaResponse>> UpdateAsync(Guid id, CmsMediaUpdateRequest request);

    /// <summary>Fails with a conflict if any banner still references this asset (see <c>ICmsMediaRepository.IsReferencedByBannerAsync</c>).</summary>
    Task<Result> DeleteAsync(Guid id);
}
