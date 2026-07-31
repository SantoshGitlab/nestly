using Nestly.Domain;

namespace Nestly.Application.Cms;

public interface ICmsMediaRepository
{
    Task<CmsMedia?> GetByIdAsync(Guid id);

    /// <summary>Every media asset, newest first - the media library backing the banner form's asset picker.</summary>
    Task<IReadOnlyList<CmsMedia>> ListAsync();

    Task AddAsync(CmsMedia media);

    Task UpdateAsync(CmsMedia media);

    Task DeleteAsync(CmsMedia media);

    /// <summary>Whether any <see cref="Banner"/> still references this asset - blocks deletion of an in-use media row (see <c>CmsMediaService.DeleteAsync</c>).</summary>
    Task<bool> IsReferencedByBannerAsync(Guid mediaId);
}
