using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Cms;
using Nestly.Application.Storage;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin CRUD over the CMS media library (SRS 12.16.2, task 124e). Writes an
/// audit entry for every mutation (task 132c gap fix), consistent with
/// <see cref="CmsPageService"/> and <see cref="CmsFaqService"/>. The entry is
/// staged before the repository call so the repository's own
/// <c>SaveChangesAsync</c> commits both in one transaction.
///
/// Task 314: <see cref="SaveFileAsync"/> is the second consumer of
/// <c>IFileStorageService</c> (the first was provider-web's job-completion
/// photo upload) - reused rather than duplicated, per its own doc comment
/// on being the documented swap point once docs/DEVOPS.md's CDN/media
/// provider OPEN DECISION resolves.
/// </summary>
public class CmsMediaService : ICmsMediaService
{
    private readonly ICmsMediaRepository _mediaRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IFileStorageService _fileStorageService;

    public CmsMediaService(ICmsMediaRepository mediaRepository, IAuditLogWriter auditLogWriter, IFileStorageService fileStorageService)
    {
        _mediaRepository = mediaRepository;
        _auditLogWriter = auditLogWriter;
        _fileStorageService = fileStorageService;
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
        await _auditLogWriter.WriteAsync(new AuditEntry("CmsMedia", media.Id.ToString(), "Created"));
        await _mediaRepository.AddAsync(media);
        return ToResponse(media);
    }

    public Task<string> SaveFileAsync(Stream content, string fileNameHint, string contentType, CancellationToken cancellationToken = default) =>
        _fileStorageService.SaveAsync(content, fileNameHint, contentType, cancellationToken);

    public async Task<Result<CmsMediaResponse>> UpdateAsync(Guid id, CmsMediaUpdateRequest request)
    {
        var media = await _mediaRepository.GetByIdAsync(id);
        if (media is null)
        {
            return Error.NotFound("CmsMedia.NotFound", "The specified media asset does not exist.");
        }

        media.Update(request.Url, request.AltText);
        await _auditLogWriter.WriteAsync(new AuditEntry("CmsMedia", media.Id.ToString(), "Updated"));
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

        await _auditLogWriter.WriteAsync(new AuditEntry("CmsMedia", media.Id.ToString(), "Deleted"));
        await _mediaRepository.DeleteAsync(media);
        return Result.Success();
    }

    private static CmsMediaResponse ToResponse(CmsMedia media) =>
        new(media.Id, media.Url, media.AltText, media.CreatedAtUtc);
}
