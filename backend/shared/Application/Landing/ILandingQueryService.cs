namespace Nestly.Application.Landing;

/// <summary>
/// Read model for the customer home page's curated sections. Inactive
/// categories/services are filtered out here rather than at curation time, so
/// deactivating a service removes it from the home page immediately without
/// the admin having to remember to un-pick it.
/// </summary>
public interface ILandingQueryService
{
    Task<HomeLandingResponse> GetHomeAsync();
}
