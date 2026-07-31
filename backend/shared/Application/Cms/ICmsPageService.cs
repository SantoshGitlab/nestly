using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cms;

/// <summary>Admin CRUD plus draft/publish workflow over static pages (SRS 12.16.1/12.16.2, task 124a/124c/124d).</summary>
public interface ICmsPageService
{
    Task<CmsPageAdminSearchResponse> SearchAsync(CmsPageAdminSearchRequest request);

    Task<Result<CmsPageResponse>> GetByIdAsync(Guid id);

    /// <summary>Creates a page, always starting as <see cref="Nestly.Domain.CmsContentStatus.Draft"/> - see <see cref="PublishAsync"/>.</summary>
    Task<Result<CmsPageResponse>> CreateAsync(CmsPageCreateRequest request);

    Task<Result<CmsPageResponse>> UpdateAsync(Guid id, CmsPageUpdateRequest request);

    Task<Result> PublishAsync(Guid id);

    Task<Result> UnpublishAsync(Guid id);
}
