using Nestly.Application.Geography;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>Public geography lookups backing location selection (SRS 11.1, 11.4.1).</summary>
public class GeographyQueryService : IGeographyQueryService
{
    private readonly IGeographyRepository _repository;

    public GeographyQueryService(IGeographyRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<CityResponse>> ListActiveCitiesAsync() =>
        _repository.ListActiveCitiesAsync();

    public async Task<Result<IReadOnlyList<LocalityResponse>>> SearchLocalitiesAsync(Guid cityId, string? search)
    {
        if (!await _repository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Geography.CityNotFound", "The specified city does not exist.");
        }

        var localities = await _repository.SearchActiveLocalitiesAsync(cityId, search);
        return Result.Success(localities);
    }
}
