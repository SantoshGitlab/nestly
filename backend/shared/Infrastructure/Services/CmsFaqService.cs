using Nestly.Application.Cms;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD plus draft/publish workflow over site-level FAQs (SRS 12.16, task 124c/124d/124f).</summary>
public class CmsFaqService : ICmsFaqService
{
    private readonly ICmsFaqRepository _faqRepository;

    public CmsFaqService(ICmsFaqRepository faqRepository)
    {
        _faqRepository = faqRepository;
    }

    public async Task<CmsFaqAdminSearchResponse> SearchAsync(CmsFaqAdminSearchRequest request)
    {
        var filter = new CmsFaqSearchFilter(request.Placement, request.Status, request.Page, request.PageSize);
        var result = await _faqRepository.SearchAsync(filter);
        return new CmsFaqAdminSearchResponse(result.Items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<CmsFaqResponse>> GetByIdAsync(Guid id)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq is null)
        {
            return Error.NotFound("CmsFaq.NotFound", "The specified FAQ does not exist.");
        }

        return CmsFaqRepository.ToResponse(faq);
    }

    public async Task<Result<CmsFaqResponse>> CreateAsync(CmsFaqCreateRequest request)
    {
        var faq = new CmsFaq(
            Guid.NewGuid(),
            request.Question,
            request.Answer,
            request.Placement,
            request.SortOrder,
            CmsContentStatus.Draft,
            request.PublishStartUtc,
            request.PublishEndUtc);

        await _faqRepository.AddAsync(faq);
        return CmsFaqRepository.ToResponse(faq);
    }

    public async Task<Result<CmsFaqResponse>> UpdateAsync(Guid id, CmsFaqUpdateRequest request)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq is null)
        {
            return Error.NotFound("CmsFaq.NotFound", "The specified FAQ does not exist.");
        }

        faq.Update(request.Question, request.Answer, request.Placement, request.SortOrder, request.PublishStartUtc, request.PublishEndUtc);
        await _faqRepository.UpdateAsync(faq);
        return CmsFaqRepository.ToResponse(faq);
    }

    public async Task<Result> PublishAsync(Guid id)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq is null)
        {
            return Result.Failure(Error.NotFound("CmsFaq.NotFound", "The specified FAQ does not exist."));
        }

        faq.Publish();
        await _faqRepository.UpdateAsync(faq);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(Guid id)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq is null)
        {
            return Result.Failure(Error.NotFound("CmsFaq.NotFound", "The specified FAQ does not exist."));
        }

        faq.Unpublish();
        await _faqRepository.UpdateAsync(faq);
        return Result.Success();
    }
}
