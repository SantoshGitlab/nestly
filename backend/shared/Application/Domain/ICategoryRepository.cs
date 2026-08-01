using Nestly.Domain;

namespace Nestly.Application;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);

    /// <summary>Whether any category (other than <paramref name="excludeId"/>, when updating) already uses this slug.</summary>
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null);

    /// <summary>Active categories with an active mapping to the given city (SRS 12.9.2), ordered for display.</summary>
    Task<IReadOnlyList<Category>> ListServiceableInCityAsync(Guid cityId);

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
}
