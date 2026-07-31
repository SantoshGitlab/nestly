using Nestly.Application.Serviceability;
using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD over the category/city serviceability mapping (SRS 12.9.2).</summary>
public interface ICategoryCityMappingRepository : IRepository<CategoryCityMapping>
{
    /// <summary>
    /// Mappings joined with category/city names for display, optionally
    /// filtered by category and/or city. Joined here rather than resolved
    /// per-row by the caller, to avoid N+1 lookups on a listing endpoint.
    /// </summary>
    Task<IReadOnlyList<CategoryCityMappingResponse>> ListAsync(Guid? categoryId, Guid? cityId);

    /// <summary>The existing mapping for this (category, city) pair, if any - the pair is unique.</summary>
    Task<CategoryCityMapping?> FindAsync(Guid categoryId, Guid cityId);
}
