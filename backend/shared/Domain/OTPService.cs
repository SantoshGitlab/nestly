using Nestly.BuildingBlocks.Results;

namespace Nestly.Domain;

public interface IOTPService
{
    // Was Task<ValidationResult> - ValidationResult's constructor is private
    // and only reachable via WithErrors(), so it can never represent success.
    // Result (the base type) can represent both, which is what generation/
    // validation actually need to report.
    Task<Result> GenerateAsync(string phoneNumber);
    Task<Result> ValidateAsync(string phoneNumber, string otpCode);
}

public interface ICustomerService
{
    Task CreateAsync(object customer);
    Task UpdateAsync(object customer);
    Task DeleteAsync(Guid id);
}
