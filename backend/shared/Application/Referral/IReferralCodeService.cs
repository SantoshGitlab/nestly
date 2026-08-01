namespace Nestly.Application.Referral;

/// <summary>
/// Referral code generation and the shareable link built from it (REFERRAL.md,
/// task 162). Generation is lazy - called the first time a customer opens the
/// Refer &amp; Earn screen (task 168's <c>GET /me/referral</c>), not at signup,
/// since most customers never share their code.
/// </summary>
public interface IReferralCodeService
{
    /// <summary>Returns the customer's existing code, or generates, persists, and returns a new unique one.</summary>
    Task<string> GetOrCreateCodeAsync(Guid customerId);

    /// <summary>Builds the full shareable link for a code (e.g. for registration deep-linking).</summary>
    string BuildShareLink(string referralCode);
}
