using Nestly.Domain;

namespace Nestly.Application;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);

    /// <summary>Whether any category (other than <paramref name="excludeId"/>, when updating) already uses this slug.</summary>
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null);

    /// <summary>
    /// Active categories with an active mapping to the given city (SRS
    /// 12.9.2), ordered for display. When <paramref name="pincodeId"/> is
    /// given, further narrowed to categories with at least one active
    /// service actually serviceable at that pincode (SRS 11.1.3 "filtered by
    /// selected city/serviceability") - the city mapping alone only says the
    /// category launched in the city, not that any of its services reach
    /// this specific area yet.
    /// </summary>
    Task<IReadOnlyList<Category>> ListServiceableInCityAsync(Guid cityId, Guid? pincodeId = null);

    /// <summary>
    /// Active categories whose name contains the query, case-insensitively
    /// (SRS 24.3 search). <paramref name="limit"/> is null (unbounded) by
    /// default because several admin lookup screens
    /// (BannerService/CouponManagementService/ServiceabilityMappingManagementService/
    /// SlotManagementService) deliberately call this with an empty query as
    /// a "list every active category" idiom and need the full set - only
    /// pass a limit for a genuine free-text search result (task 136c).
    /// </summary>
    Task<IReadOnlyList<Category>> SearchActiveAsync(string query, int? limit = null);

    /// <summary>Every category regardless of active status, ordered for the admin management screen (SRS 12.5.1).</summary>
    Task<IReadOnlyList<Category>> ListAllAsync();

    /// <summary>Names for a set of category ids in one round trip (task 256) - mirrors <c>ICustomerRepository.GetNamesByIdsAsync</c>.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Full categories for a set of ids, in one query. The name-only
    /// <see cref="GetNamesByIdsAsync"/> is not enough for callers that also
    /// need slug/imagery (e.g. assembling the curated home page).
    /// </summary>
    Task<IReadOnlyList<Category>> ListByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Active subcategories of a parent category (Phase 3 catalog redesign), ordered for display.</summary>
    Task<IReadOnlyList<Category>> ListChildrenAsync(Guid parentCategoryId);

    /// <summary>Whether any category still points at this group - the delete-guard for <c>CategoryGroupManagementService</c>, mirrors <c>IServiceRepository.ExistsByServiceGroupIdAsync</c>.</summary>
    Task<bool> ExistsByCategoryGroupIdAsync(Guid categoryGroupId);
}
