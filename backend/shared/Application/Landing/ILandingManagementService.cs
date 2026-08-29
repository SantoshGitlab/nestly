using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Landing;

/// <summary>
/// Admin curation of the customer home page's three configurable sections.
/// Every mutation replaces a whole section (or one category strip) rather
/// than editing rows, so the submitted order is the display order and a
/// repeated save is idempotent.
/// </summary>
public interface ILandingManagementService
{
    /// <summary>The full config for the admin screen - all three sections in one call.</summary>
    Task<LandingConfigResponse> GetConfigAsync();

    /// <summary>Replaces the New &amp; Trending sub-category picks. Rejects ids that are not real sub-categories.</summary>
    Task<Result> UpdateNewAndTrendingAsync(UpdateNewAndTrendingRequest request);

    /// <summary>Replaces the Most Booked service picks. Rejects ids that are not real services.</summary>
    Task<Result> UpdateMostBookedAsync(UpdateMostBookedRequest request);

    /// <summary>
    /// Replaces one heading category's strip. Rejects an unknown category, an
    /// unknown service, a service that does not belong to that category, or
    /// more than <see cref="Nestly.Domain.LandingSelection.MaxServicesPerCategorySection"/> picks.
    /// </summary>
    Task<Result> UpdateCategorySectionAsync(Guid categoryId, UpdateCategorySectionRequest request);
}
