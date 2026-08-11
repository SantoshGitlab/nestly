using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD + read access over a category's optional service-group section headers.</summary>
public interface IServiceGroupRepository : IRepository<ServiceGroup>
{
    Task DeleteAsync(ServiceGroup entity);

    /// <summary>Every group, optionally filtered to one category, ordered for the admin management screen.</summary>
    Task<IReadOnlyList<ServiceGroup>> ListAllAsync(Guid? categoryId);

    /// <summary>
    /// Active groups across every category in <paramref name="categoryIds"/>,
    /// in one query - the batch counterpart to <see cref="ListAllAsync"/> for
    /// callers assembling a category detail response, mirroring
    /// <c>IServiceVariantRepository.ListActiveByServiceIdsAsync</c>.
    /// </summary>
    Task<IReadOnlyList<ServiceGroup>> ListActiveByCategoryIdsAsync(IReadOnlyCollection<Guid> categoryIds);
}
