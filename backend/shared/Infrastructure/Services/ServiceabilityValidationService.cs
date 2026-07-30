using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>Serviceability validation (SRS 12.9.2, task 43), read-cached per task 49.</summary>
public class ServiceabilityValidationService : IServiceabilityValidationService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly IServiceabilityRepository _repository;
    private readonly ICacheService _cache;

    public ServiceabilityValidationService(IServiceabilityRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<Result<bool>> IsCategoryServiceableAsync(Guid categoryId, Guid cityId)
    {
        if (!await _repository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Serviceability.CityNotFound", "The specified city does not exist.");
        }

        bool isActive = await _cache.GetOrCreateAsync(
            CacheKeys.CategoryServiceability(categoryId, cityId),
            _ => _repository.IsCategoryActiveInCityAsync(categoryId, cityId),
            Ttl);

        return isActive;
    }

    public async Task<Result<bool>> IsServiceServiceableByPincodeAsync(Guid serviceId, Guid pincodeId)
    {
        if (!await _repository.PincodeExistsAsync(pincodeId))
        {
            return Error.NotFound("Serviceability.PincodeNotFound", "The specified pincode does not exist.");
        }

        bool isActive = await _cache.GetOrCreateAsync(
            CacheKeys.ServicePincodeServiceability(serviceId, pincodeId),
            _ => _repository.IsServiceActiveInPincodeAsync(serviceId, pincodeId),
            Ttl);

        return isActive;
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
