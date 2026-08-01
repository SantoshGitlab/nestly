namespace Nestly.Application.PartnerIdentity;

/// <summary>Step 1 of partner registration: request an OTP be sent to a mobile number.</summary>
public record RequestPartnerRegistrationOtpRequest(string Mobile);

/// <summary>
/// Step 2 of partner registration: the OTP proves ownership of the mobile
/// number (mirrors <c>RegisterCustomerRequest</c>). Unlike customer
/// registration, there is no optional email+password mode - PARTNER.md's API
/// surface lists only OTP-based auth for partners.
/// </summary>
public record RegisterPartnerRequest(
    string Mobile,
    string OtpCode,
    string LegalName,
    string DisplayName,
    string? Email,
    bool ConsentAccepted);

/// <summary>Never includes anything auth-sensitive.</summary>
public record PartnerSummaryResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    string Status,
    string OnboardingStatus);
