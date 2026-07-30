using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Combined category/service search (task 42c, SRS 11.5-11.6, 24.3).</summary>
public interface ICatalogSearchService
{
    Task<Result<CatalogSearchResponse>> SearchAsync(string query);
}
