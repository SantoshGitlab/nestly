namespace Nestly.Application.Identity;

/// <summary>
/// Step 1 of the reset flow (SRS 11.2.2: "Forgot password / reset password
/// flow shall be supported if password login exists"). Keyed on the email
/// because that is the identifier of the email+password auth identity being
/// reset; the verification code is delivered to the mobile number already
/// proven at registration, which the caller never has to supply or see.
/// </summary>
public record ForgotPasswordRequest(string Email);

/// <summary>
/// Step 2: the OTP proves the requester controls the account's verified
/// mobile number, which is what authorises replacing the password hash.
/// </summary>
public record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);
