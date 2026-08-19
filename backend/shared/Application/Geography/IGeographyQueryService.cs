using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Geography;

/// <summary>Public geography lookups backing location selection (SRS 11.1, 11.4.1).</summary>
public interface IGeographyQueryService
{
    Task<IReadOnlyList<CityResponse>> ListActiveCitiesAsync();

    Task<Result<IReadOnlyList<LocalityResponse>>> SearchLocalitiesAsync(Guid cityId, string? search);

    /// <summary>Resolves a pincode's city/state for autofilling an address form (task 369). NotFound if no active pincode matches.</summary>
    Task<Result<PincodeLookupResponse>> ResolvePincodeAsync(string pincodeCode);
}
