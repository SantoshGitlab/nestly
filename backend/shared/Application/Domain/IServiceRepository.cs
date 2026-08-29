using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceRepository : IRepository<Service>
{
    /// <summary>Active services under a category, ordered for display.</summary>
    Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId);

    Task<Service?> GetBySlugAsync(string slug);

    /// <summary>Whether any service (other than <paramref name="excludeId"/>, when updating) already uses this slug.</summary>
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null);

    /// <summary>
    /// Active services whose name contains the query, case-insensitively
    /// (SRS 24.3 search). <paramref name="limit"/> is null (unbounded) by
    /// default because ServiceabilityMappingManagementService/
    /// SlotManagementService deliberately call this with an empty query as a
    /// "list every active service" idiom and need the full set - only pass a
    /// limit for a genuine free-text search result (task 136c).
    /// </summary>
    Task<IReadOnlyList<Service>> SearchActiveAsync(string query, int? limit = null);

    /// <summary>Names for a set of service ids in one round trip (task 256) - mirrors <c>ICustomerRepository.GetNamesByIdsAsync</c>.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Full services for a set of ids, in one query. The name-only
    /// <see cref="GetNamesByIdsAsync"/> is not enough for callers that also
    /// need slug/price/imagery (e.g. assembling the curated home page).
    /// </summary>
    Task<IReadOnlyList<Service>> ListByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Every service regardless of active status, optionally filtered to one
    /// category, ordered for the admin management screen (SRS 12.6.1).
    /// </summary>
    Task<IReadOnlyList<Service>> ListAllAsync(Guid? categoryId);

    /// <summary>Every service regardless of category or active status, for the admin base-price management screen (SRS 12.8.1).</summary>
    Task<IReadOnlyList<Service>> ListAllAsync();

    /// <summary>Whether any service still references this service group - mirrors <c>IServiceAddOnRepository.ExistsByGroupIdAsync</c>, used to block deleting an in-use group.</summary>
    Task<bool> ExistsByServiceGroupIdAsync(Guid serviceGroupId);
}
