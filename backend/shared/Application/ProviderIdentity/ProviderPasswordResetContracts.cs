namespace Nestly.Application.ProviderIdentity;

/// <summary>
/// Step 1 of the reset flow (task 372, mirrors <c>ForgotPasswordRequest</c>).
/// Keyed on the email because that is the identifier of the email+password
/// auth identity being reset; the verification code is delivered to the
/// mobile number already proven at registration, which the caller never has
/// to supply or see.
/// </summary>
public record ForgotProviderPasswordRequest(string Email);

/// <summary>
/// Step 2: the OTP proves the requester controls the account's verified
/// mobile number, which is what authorises replacing the password hash.
/// </summary>
public record ResetProviderPasswordRequest(string Email, string OtpCode, string NewPassword);
