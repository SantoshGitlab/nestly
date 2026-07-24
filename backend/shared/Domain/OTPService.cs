using Nestly.BuildingBlocks.Results;

namespace Nestly.Domain;

public interface IOTPService
{
    Task<ValidationResult> GenerateAsync(string phoneNumber);
    Task<ValidationResult> ValidateAsync(string phoneNumber, string otpCode);
}

public interface ICustomerService
{
    Task CreateAsync(object customer);
    Task UpdateAsync(object customer);
    Task DeleteAsync(Guid id);
}
