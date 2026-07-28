namespace Nestly.Application.Profile;

/// <summary>
/// The customer's own profile (SRS 11.2.3 "View profile"). Contains no
/// credential material — password hashes and session tokens never leave the
/// persistence layer.
/// </summary>
public record CustomerProfileResponse(
    Guid Id,
    string Mobile,
    string? Email,
    string Name,
    DateTime? DateOfBirth,
    string? City,
    string? State,
    string? Pincode,
    string? Country,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// SRS 11.2.3 "Edit name, email, optional profile data". Mobile and email are
/// absent by design: both are identity-bearing and change only through the
/// re-verified endpoints below.
/// </summary>
public record UpdateProfileRequest(
    string Name,
    DateTime? DateOfBirth,
    string? City,
    string? State,
    string? Pincode,
    string? Country);

/// <summary>Step 1 of a mobile change: send a code to the number being claimed.</summary>
public record RequestMobileChangeOtpRequest(string NewMobile);

/// <summary>Step 2: the code proves the new number is reachable by this customer.</summary>
public record ConfirmMobileChangeRequest(string NewMobile, string OtpCode);

/// <summary>Step 1 of an email change: send a code to the address being claimed.</summary>
public record RequestEmailChangeOtpRequest(string NewEmail);

/// <summary>Step 2: the code proves the new address is reachable by this customer.</summary>
public record ConfirmEmailChangeRequest(string NewEmail, string OtpCode);

/// <summary>
/// Per-channel messaging consent (SRS 11.2.3, channels per SRS 30.2). Every
/// flag is explicit rather than nullable so a PUT always states the full
/// desired state and cannot leave a channel ambiguously unset.
/// </summary>
public record CommunicationPreferencesRequest(
    bool TransactionalSms,
    bool TransactionalEmail,
    bool TransactionalWhatsApp,
    bool PromotionalSms,
    bool PromotionalEmail,
    bool PromotionalWhatsApp,
    bool Push);

public record CommunicationPreferencesResponse(
    bool TransactionalSms,
    bool TransactionalEmail,
    bool TransactionalWhatsApp,
    bool PromotionalSms,
    bool PromotionalEmail,
    bool PromotionalWhatsApp,
    bool Push,
    DateTime UpdatedAt);
