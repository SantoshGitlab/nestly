using Nestly.Application;
using Nestly.Application.Cms;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD plus draft/publish workflow over banners (SRS 12.16, task 124b/124c/124d/124f).</summary>
public class BannerService : IBannerService
{
    private readonly IBannerRepository _bannerRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICmsMediaRepository _mediaRepository;

    public BannerService(
        IBannerRepository bannerRepository,
        ICategoryRepository categoryRepository,
        ICmsMediaRepository mediaRepository)
    {
        _bannerRepository = bannerRepository;
        _categoryRepository = categoryRepository;
        _mediaRepository = mediaRepository;
    }

    public async Task<IReadOnlyList<CategoryLookupResponse>> ListCategoriesAsync()
    {
        var categories = await _categoryRepository.SearchActiveAsync(string.Empty);
        return categories.Select(c => new CategoryLookupResponse(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<CmsMediaResponse>> ListMediaAsync()
    {
        var media = await _mediaRepository.ListAsync();
        return media.Select(m => new CmsMediaResponse(m.Id, m.Url, m.AltText, m.CreatedAtUtc)).ToList();
    }

    public async Task<BannerAdminSearchResponse> SearchAsync(BannerAdminSearchRequest request)
    {
        var filter = new BannerSearchFilter(request.Placement, request.Status, request.CategoryId, request.Page, request.PageSize);
        var result = await _bannerRepository.SearchAsync(filter);
        return new BannerAdminSearchResponse(result.Items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<BannerResponse>> GetByIdAsync(Guid id)
    {
        var banner = await _bannerRepository.GetByIdAsync(id);
        if (banner is null)
        {
            return Error.NotFound("Banner.NotFound", "The specified banner does not exist.");
        }

        return await ToResponseAsync(banner);
    }

    public async Task<Result<BannerResponse>> CreateAsync(BannerCreateRequest request)
    {
        var referenceError = await ValidateReferencesAsync(request.MediaId, request.Placement, request.CategoryId);
        if (referenceError is not null)
        {
            return referenceError;
        }

        var banner = new Banner(
            Guid.NewGuid(),
            request.Title,
            request.MediaId,
            request.LinkUrl,
            request.Placement,
            request.CategoryId,
            request.SortOrder,
            CmsContentStatus.Draft,
            request.PublishStartUtc,
            request.PublishEndUtc);

        await _bannerRepository.AddAsync(banner);
        return await ToResponseAsync(banner);
    }

    public async Task<Result<BannerResponse>> UpdateAsync(Guid id, BannerUpdateRequest request)
    {
        var banner = await _bannerRepository.GetByIdAsync(id);
        if (banner is null)
        {
            return Error.NotFound("Banner.NotFound", "The specified banner does not exist.");
        }

        var referenceError = await ValidateReferencesAsync(request.MediaId, request.Placement, request.CategoryId);
        if (referenceError is not null)
        {
            return referenceError;
        }

        banner.Update(
            request.Title,
            request.MediaId,
            request.LinkUrl,
            request.Placement,
            request.CategoryId,
            request.SortOrder,
            request.PublishStartUtc,
            request.PublishEndUtc);

        await _bannerRepository.UpdateAsync(banner);
        return await ToResponseAsync(banner);
    }

    public async Task<Result> PublishAsync(Guid id)
    {
        var banner = await _bannerRepository.GetByIdAsync(id);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("Banner.NotFound", "The specified banner does not exist."));
        }

        banner.Publish();
        await _bannerRepository.UpdateAsync(banner);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(Guid id)
    {
        var banner = await _bannerRepository.GetByIdAsync(id);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("Banner.NotFound", "The specified banner does not exist."));
        }

        banner.Unpublish();
        await _bannerRepository.UpdateAsync(banner);
        return Result.Success();
    }

    /// <summary>Checks the two cross-aggregate references a Banner carries - the media asset always, the category only when placement is CategoryPage - which the entity itself cannot verify without repository access. Returns null when both are valid.</summary>
    private async Task<Result<BannerResponse>?> ValidateReferencesAsync(Guid mediaId, CmsPlacement placement, Guid? categoryId)
    {
        if (await _mediaRepository.GetByIdAsync(mediaId) is null)
        {
            return Error.NotFound("Banner.MediaNotFound", "The specified media asset does not exist.");
        }

        if (placement == CmsPlacement.CategoryPage)
        {
            if (!categoryId.HasValue || await _categoryRepository.GetByIdAsync(categoryId.Value) is null)
            {
                return Error.NotFound("Banner.CategoryNotFound", "The specified category does not exist.");
            }
        }

        return null;
    }

    private async Task<BannerResponse> ToResponseAsync(Banner banner)
    {
        var media = await _mediaRepository.GetByIdAsync(banner.MediaId);

        string? categoryName = null;
        if (banner.CategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(banner.CategoryId.Value);
            categoryName = category?.Name;
        }

        return new BannerResponse(
            banner.Id,
            banner.Title,
            banner.MediaId,
            media?.Url ?? string.Empty,
            banner.LinkUrl,
            banner.Placement,
            banner.CategoryId,
            categoryName,
            banner.SortOrder,
            banner.Status,
            banner.PublishStartUtc,
            banner.PublishEndUtc,
            banner.CreatedAtUtc,
            banner.UpdatedAtUtc);
    }
}
