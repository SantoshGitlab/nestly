using Nestly.Application.Cms;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD plus draft/publish workflow over static pages (SRS 12.16, task 124a/124c/124d/124f).</summary>
public class CmsPageService : ICmsPageService
{
    private readonly ICmsPageRepository _pageRepository;

    public CmsPageService(ICmsPageRepository pageRepository)
    {
        _pageRepository = pageRepository;
    }

    public async Task<CmsPageAdminSearchResponse> SearchAsync(CmsPageAdminSearchRequest request)
    {
        var filter = new CmsPageSearchFilter(request.Title, request.Slug, request.Status, request.Placement, request.Page, request.PageSize);
        var result = await _pageRepository.SearchAsync(filter);
        return new CmsPageAdminSearchResponse(result.Items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<CmsPageResponse>> GetByIdAsync(Guid id)
    {
        var page = await _pageRepository.GetByIdAsync(id);
        if (page is null)
        {
            return Error.NotFound("CmsPage.NotFound", "The specified page does not exist.");
        }

        return CmsPageRepository.ToResponse(page);
    }

    public async Task<Result<CmsPageResponse>> CreateAsync(CmsPageCreateRequest request)
    {
        if (await _pageRepository.SlugExistsAsync(request.Slug))
        {
            return Error.Conflict("CmsPage.SlugAlreadyExists", "A page with this slug already exists.");
        }

        var page = new CmsPage(
            Guid.NewGuid(),
            request.Title,
            request.Slug,
            request.Body,
            request.SeoTitle,
            request.SeoDescription,
            request.SeoKeywords,
            request.Placement,
            CmsContentStatus.Draft,
            request.PublishStartUtc,
            request.PublishEndUtc);

        await _pageRepository.AddAsync(page);
        return CmsPageRepository.ToResponse(page);
    }

    public async Task<Result<CmsPageResponse>> UpdateAsync(Guid id, CmsPageUpdateRequest request)
    {
        var page = await _pageRepository.GetByIdAsync(id);
        if (page is null)
        {
            return Error.NotFound("CmsPage.NotFound", "The specified page does not exist.");
        }

        if (await _pageRepository.SlugExistsAsync(request.Slug, id))
        {
            return Error.Conflict("CmsPage.SlugAlreadyExists", "A page with this slug already exists.");
        }

        page.Update(
            request.Title,
            request.Slug,
            request.Body,
            request.SeoTitle,
            request.SeoDescription,
            request.SeoKeywords,
            request.Placement,
            request.PublishStartUtc,
            request.PublishEndUtc);

        await _pageRepository.UpdateAsync(page);
        return CmsPageRepository.ToResponse(page);
    }

    public async Task<Result> PublishAsync(Guid id)
    {
        var page = await _pageRepository.GetByIdAsync(id);
        if (page is null)
        {
            return Result.Failure(Error.NotFound("CmsPage.NotFound", "The specified page does not exist."));
        }

        page.Publish();
        await _pageRepository.UpdateAsync(page);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(Guid id)
    {
        var page = await _pageRepository.GetByIdAsync(id);
        if (page is null)
        {
            return Result.Failure(Error.NotFound("CmsPage.NotFound", "The specified page does not exist."));
        }

        page.Unpublish();
        await _pageRepository.UpdateAsync(page);
        return Result.Success();
    }
}
