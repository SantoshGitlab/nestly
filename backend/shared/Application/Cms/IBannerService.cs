using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cms;

/// <summary>Admin CRUD plus draft/publish workflow over banners (SRS 12.16.1/12.16.2, task 124b/124c/124d/124f).</summary>
public interface IBannerService
{
    /// <summary>Active categories, for the banner form's "category" picker when placement is CategoryPage - same reasoning as <c>ICouponManagementService.ListApplicableCategoriesAsync</c>'s doc comment.</summary>
    Task<IReadOnlyList<CategoryLookupResponse>> ListCategoriesAsync();

    /// <summary>The media library, for the banner form's asset picker (task 124e).</summary>
    Task<IReadOnlyList<CmsMediaResponse>> ListMediaAsync();

    Task<BannerAdminSearchResponse> SearchAsync(BannerAdminSearchRequest request);

    Task<Result<BannerResponse>> GetByIdAsync(Guid id);

    /// <summary>Creates a banner, always starting as <see cref="Nestly.Domain.CmsContentStatus.Draft"/> - see <see cref="PublishAsync"/>.</summary>
    Task<Result<BannerResponse>> CreateAsync(BannerCreateRequest request);

    Task<Result<BannerResponse>> UpdateAsync(Guid id, BannerUpdateRequest request);

    Task<Result> PublishAsync(Guid id);

    Task<Result> UnpublishAsync(Guid id);
}
