using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Geography master CRUD over states (SRS 12.9.1).</summary>
public interface IStateRepository : IRepository<State>
{
    /// <summary>All states, alphabetically ordered, for the geography master admin screen.</summary>
    Task<IReadOnlyList<State>> ListAsync();

    /// <summary>Whether another state already uses this code (State.Code is globally unique).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null);
}
