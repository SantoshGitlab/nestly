using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>Serviceability validation (SRS 12.9.2, task 43).</summary>
public class ServiceabilityValidationService : IServiceabilityValidationService
{
    private readonly IServiceabilityRepository _repository;

    public ServiceabilityValidationService(IServiceabilityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> IsCategoryServiceableAsync(Guid categoryId, Guid cityId)
    {
        if (!await _repository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Serviceability.CityNotFound", "The specified city does not exist.");
        }

        return await _repository.IsCategoryActiveInCityAsync(categoryId, cityId);
    }

    public async Task<Result<bool>> IsServiceServiceableByPincodeAsync(Guid serviceId, Guid pincodeId)
    {
        if (!await _repository.PincodeExistsAsync(pincodeId))
        {
            return Error.NotFound("Serviceability.PincodeNotFound", "The specified pincode does not exist.");
        }

        return await _repository.IsServiceActiveInPincodeAsync(serviceId, pincodeId);
    }

    public async Task<Result<bool>> IsServiceServiceableByLocalityAsync(Guid serviceId, Guid localityId)
    {
        var pincodeId = await _repository.GetPincodeIdForLocalityAsync(localityId);
        if (pincodeId is null)
        {
            return Error.NotFound("Serviceability.LocalityNotFound", "The specified locality does not exist.");
        }

        return await _repository.IsServiceActiveInPincodeAsync(serviceId, pincodeId.Value);
    }
}
