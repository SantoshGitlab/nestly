using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cms;

/// <summary>Admin CRUD plus draft/publish workflow over site-level FAQs (SRS 12.16.1/12.16.2, task 124c/124d). Distinct from per-service FAQ management (task 40e) - see <see cref="Nestly.Domain.CmsFaq"/>'s doc comment.</summary>
public interface ICmsFaqService
{
    Task<CmsFaqAdminSearchResponse> SearchAsync(CmsFaqAdminSearchRequest request);

    Task<Result<CmsFaqResponse>> GetByIdAsync(Guid id);

    /// <summary>Creates a FAQ entry, always starting as <see cref="Nestly.Domain.CmsContentStatus.Draft"/> - see <see cref="PublishAsync"/>.</summary>
    Task<Result<CmsFaqResponse>> CreateAsync(CmsFaqCreateRequest request);

    Task<Result<CmsFaqResponse>> UpdateAsync(Guid id, CmsFaqUpdateRequest request);

    Task<Result> PublishAsync(Guid id);

    Task<Result> UnpublishAsync(Guid id);
}
