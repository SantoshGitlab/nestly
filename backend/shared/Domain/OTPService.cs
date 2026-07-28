using Nestly.BuildingBlocks.Results;

namespace Nestly.Domain;

public interface IOTPService
{
    // Was Task<ValidationResult> - ValidationResult's constructor is private
    // and only reachable via WithErrors(), so it can never represent success.
    // Result (the base type) can represent both, which is what generation/
    // validation actually need to report.
    //
    // Purpose is required on both ends (not just stored) so an OTP issued for
    // login cannot be replayed to satisfy registration or password-reset -
    // SRS 28.3 "replay / OTP brute force".
    Task<Result> GenerateAsync(string phoneNumber, OtpPurpose purpose);
    Task<Result> ValidateAsync(string phoneNumber, string otpCode, OtpPurpose purpose);
}

public interface ICustomerService
{
    Task CreateAsync(object customer);
    Task UpdateAsync(object customer);
    Task DeleteAsync(Guid id);
}
