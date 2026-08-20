namespace Nestly.Application.ProviderIdentity;

/// <summary>Step 1 of provider registration: request an OTP be sent to a mobile number.</summary>
public record RequestProviderRegistrationOtpRequest(string Mobile);

/// <summary>
/// Step 2 of provider registration: the OTP proves ownership of the mobile
/// number (mirrors <c>RegisterCustomerRequest</c>). Task 372 added the same
/// optional email+password mode customer registration already had - when
/// <see cref="Password"/> is supplied, an additional email+password auth
/// identity is created alongside the always-present mobile identity.
/// </summary>
public record RegisterProviderRequest(
    string Mobile,
    string OtpCode,
    string LegalName,
    string DisplayName,
    string? Email,
    bool ConsentAccepted,
    string? Password = null);

/// <summary>Step 1 of email-first provider registration: request an OTP be sent to an email address.</summary>
public record RequestProviderRegistrationEmailOtpRequest(string Email);

/// <summary>
/// Step 2 of email-first provider registration: the OTP proves ownership of
/// the email address instead of the mobile number (mirrors
/// <see cref="RegisterProviderRequest"/>). Mobile is still collected and
/// stored, but only an <c>EmailPassword</c> auth identity is created since
/// mobile was never OTP-verified on this path.
/// </summary>
public record RegisterProviderWithEmailRequest(
    string Email,
    string OtpCode,
    string LegalName,
    string DisplayName,
    string Mobile,
    string Password,
    bool ConsentAccepted);

/// <summary>Never includes anything auth-sensitive.</summary>
public record ProviderSummaryResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    string Status,
    string OnboardingStatus);
