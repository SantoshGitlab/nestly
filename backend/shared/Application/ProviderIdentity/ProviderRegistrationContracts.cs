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

/// <summary>Never includes anything auth-sensitive.</summary>
public record ProviderSummaryResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    string Status,
    string OnboardingStatus);
