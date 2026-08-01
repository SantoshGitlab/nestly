namespace Nestly.Application.Identity;

/// <summary>Step 1 of registration: request an OTP be sent to a mobile number.</summary>
public record RequestRegistrationOtpRequest(string Mobile);

/// <summary>
/// Step 2 of registration (SRS 11.2.1): the OTP proves ownership of the
/// mobile number. Email+password is optional — when <see cref="Password"/>
/// is supplied, an additional email+password auth identity is created
/// alongside the always-present mobile identity.
/// </summary>
public record RegisterCustomerRequest(
    string Mobile,
    string OtpCode,
    string Name,
    string? Email,
    string? Password,
    bool ConsentAccepted,
    string? ReferralCode = null);

/// <summary>Never includes PasswordHash or anything else sensitive.</summary>
public record CustomerSummaryResponse(Guid Id, string Mobile, string? Email, string Name, string Status);
