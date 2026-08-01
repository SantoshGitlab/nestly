using Nestly.Application;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public service catalog queries (tasks 42a/42b/52d/52f), read-cached per task 49.</summary>
public class ServiceQueryService : IServiceQueryService
{
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(15);

    /// <summary>How many of a service's most recent reviews are shown alongside the rating summary (task 52f).</summary>
    private const int RecentReviewCount = 5;

    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceFaqRepository _faqRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly ICacheService _cache;

    public ServiceQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceFaqRepository faqRepository,
        IReviewRepository reviewRepository,
        ICacheService cache)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _faqRepository = faqRepository;
        _reviewRepository = reviewRepository;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<ServiceListItemResponse>>> ListByCategoryAsync(Guid categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category is null || !category.IsActive)
        {
            return Error.NotFound("Catalog.CategoryNotFound", "The specified category does not exist.");
        }

        IReadOnlyList<ServiceListItemResponse> response = await _cache.GetOrCreateAsync(
            CacheKeys.ServicesByCategory(categoryId),
            async _ =>
            {
                var services = await _serviceRepository.ListActiveByCategoryAsync(categoryId);
                return (IReadOnlyList<ServiceListItemResponse>)services.Select(ToListItem).ToList();
            },
            ListTtl);

        return Result.Success(response);
    }

    public async Task<Result<ServiceDetailResponse>> GetDetailBySlugAsync(string slug)
    {
        // Slug -> id is a cheap unique-index hit and always fresh; only the
        // expensive category-breadcrumb + add-ons assembly below is cached.
        var service = await _serviceRepository.GetBySlugAsync(slug);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Catalog.ServiceNotFound", "The specified service does not exist.");
        }

        var detail = await _cache.GetOrCreateAsync(
            CacheKeys.Service(service.Id),
            async _ =>
            {
                var category = await _categoryRepository.GetByIdAsync(service.CategoryId);
                var addOns = await _addOnRepository.ListActiveByServiceAsync(service.Id);
                var faqs = await _faqRepository.ListByServiceAsync(service.Id);

                return new ServiceDetailResponse(
                    service.Id,
                    service.Name,
                    service.Slug,
                    service.Description,
                    service.Price,
                    service.Inclusions,
                    service.Exclusions,
                    service.CancellationPolicy,
                    service.ReschedulePolicy,
                    service.CategoryId,
                    category?.Name ?? string.Empty,
                    category?.Slug ?? string.Empty,
                    addOns.Select(ToAddOnSummary).ToList(),
                    faqs.Select(ToFaqResponse).ToList());
            },
            DetailTtl);

        return detail;
    }

    public async Task<Result<ServiceReviewSummaryResponse>> GetReviewSummaryBySlugAsync(string slug)
    {
        var service = await _serviceRepository.GetBySlugAsync(slug);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Catalog.ServiceNotFound", "The specified service does not exist.");
        }

        // Not cached (unlike the detail/list queries above) - a customer who
        // just submitted a review reasonably expects to see it reflected
        // immediately, and this is a single indexed query, not worth trading
        // that freshness away for.
        var reviews = await _reviewRepository.ListByServiceAsync(service.Id);

        var breakdown = Enumerable.Range(1, 5).ToDictionary(rating => rating, _ => 0);
        foreach (var review in reviews)
        {
            breakdown[review.Rating]++;
        }

        double averageRating = reviews.Count == 0 ? 0 : reviews.Average(r => r.Rating);

        return new ServiceReviewSummaryResponse(
            Math.Round(averageRating, 2),
            reviews.Count,
            breakdown,
            reviews.Take(RecentReviewCount).Select(ToReviewItem).ToList());
    }

    private static ServiceListItemResponse ToListItem(Service service) => new(
        service.Id,
        service.Name,
        service.Slug,
        service.Description,
        service.Price);

    private static ServiceAddOnSummaryResponse ToAddOnSummary(ServiceAddOn addOn) => new(
        addOn.Id,
        addOn.Name,
        addOn.Description,
        addOn.Price);

    private static ServiceFaqResponse ToFaqResponse(ServiceFaq faq) => new(faq.Id, faq.Question, faq.Answer);

    private static ServiceReviewItemResponse ToReviewItem(Review review) => new(
        review.Id, review.Rating, review.ReviewText, review.CreatedAtUtc);
}
