using Nestly.Application.Cms;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over the CMS media library (SRS 12.16.2, task 124e).</summary>
public class CmsMediaService : ICmsMediaService
{
    private readonly ICmsMediaRepository _mediaRepository;

    public CmsMediaService(ICmsMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task<IReadOnlyList<CmsMediaResponse>> ListAsync()
    {
        var media = await _mediaRepository.ListAsync();
        return media.Select(ToResponse).ToList();
    }

    public async Task<Result<CmsMediaResponse>> GetByIdAsync(Guid id)
    {
        var media = await _mediaRepository.GetByIdAsync(id);
        if (media is null)
        {
            return Error.NotFound("CmsMedia.NotFound", "The specified media asset does not exist.");
        }

        return ToResponse(media);
    }

    public async Task<Result<CmsMediaResponse>> CreateAsync(CmsMediaCreateRequest request)
    {
        var media = new CmsMedia(Guid.NewGuid(), request.Url, request.AltText);
        await _mediaRepository.AddAsync(media);
        return ToResponse(media);
    }

    public async Task<Result<CmsMediaResponse>> UpdateAsync(Guid id, CmsMediaUpdateRequest request)
    {
        var media = await _mediaRepository.GetByIdAsync(id);
        if (media is null)
        {
            return Error.NotFound("CmsMedia.NotFound", "The specified media asset does not exist.");
        }

        media.Update(request.Url, request.AltText);
        await _mediaRepository.UpdateAsync(media);
        return ToResponse(media);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var media = await _mediaRepository.GetByIdAsync(id);
        if (media is null)
        {
            return Result.Failure(Error.NotFound("CmsMedia.NotFound", "The specified media asset does not exist."));
        }

        if (await _mediaRepository.IsReferencedByBannerAsync(id))
        {
            return Result.Failure(Error.Conflict("CmsMedia.InUse", "This media asset is referenced by at least one banner and cannot be deleted."));
        }

        await _mediaRepository.DeleteAsync(media);
        return Result.Success();
    }

    private static CmsMediaResponse ToResponse(CmsMedia media) =>
        new(media.Id, media.Url, media.AltText, media.CreatedAtUtc);
}
