using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD + read access over a category's optional subcategory-group section headers.</summary>
public interface ICategoryGroupRepository : IRepository<CategoryGroup>
{
    Task DeleteAsync(CategoryGroup entity);

    /// <summary>Every group, optionally filtered to one parent category, ordered for the admin management screen.</summary>
    Task<IReadOnlyList<CategoryGroup>> ListAllAsync(Guid? categoryId);

    /// <summary>
    /// Active groups across every parent category in
    /// <paramref name="categoryIds"/>, in one query - the batch counterpart
    /// to <see cref="ListAllAsync"/> for callers assembling a category detail
    /// response, mirroring <c>IServiceGroupRepository.ListActiveByCategoryIdsAsync</c>.
    /// </summary>
    Task<IReadOnlyList<CategoryGroup>> ListActiveByCategoryIdsAsync(IReadOnlyCollection<Guid> categoryIds);
}
