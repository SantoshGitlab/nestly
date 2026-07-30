using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Serviceability;

/// <summary>
/// Validates whether a category or service is serviceable at a given
/// geography (SRS 12.9.2). The boolean payload is a business fact, not an
/// error - "not serviceable" is a normal, expected outcome. Failure is
/// reserved for the referenced city/pincode/locality not existing.
/// </summary>
public interface IServiceabilityValidationService
{
    Task<Result<bool>> IsCategoryServiceableAsync(Guid categoryId, Guid cityId);

    Task<Result<bool>> IsServiceServiceableByPincodeAsync(Guid serviceId, Guid pincodeId);

    Task<Result<bool>> IsServiceServiceableByLocalityAsync(Guid serviceId, Guid localityId);
}
