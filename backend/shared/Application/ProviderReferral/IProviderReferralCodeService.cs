namespace Nestly.Application.ProviderReferral;

/// <summary>
/// Referral code generation and the shareable link built from it, mirrors
/// <c>IReferralCodeService</c>. Generation is lazy - called the first time a
/// provider opens the Refer &amp; Earn screen, not at signup.
/// </summary>
public interface IProviderReferralCodeService
{
    /// <summary>Returns the provider's existing code, or generates, persists, and returns a new unique one.</summary>
    Task<string> GetOrCreateCodeAsync(Guid providerId);

    /// <summary>Builds the full shareable link for a code (e.g. for registration deep-linking).</summary>
    string BuildShareLink(string referralCode);
}
