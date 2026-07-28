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
    //
    // `target` is the contact detail being proven — a mobile number for the
    // SMS channel, an email address for the Email channel. Email delivery
    // exists for the change-email re-verification flow (SRS 11.2.3), where
    // the address under test is by definition not yet on the account and so
    // cannot be reached by SMS. Channel defaults to SMS, which is what every
    // pre-existing caller (registration, login, password reset) uses.
    Task<Result> GenerateAsync(string target, OtpPurpose purpose, NotificationChannel channel = NotificationChannel.Sms);
    Task<Result> ValidateAsync(string target, string otpCode, OtpPurpose purpose);
}

public interface ICustomerService
{
    Task CreateAsync(object customer);
    Task UpdateAsync(object customer);
    Task DeleteAsync(Guid id);
}
