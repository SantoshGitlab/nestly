namespace Nestly.Application.ProviderReferral;

/// <summary>Backs provider-api's Refer &amp; Earn screen, mirrors IReferralCustomerService.</summary>
public interface IProviderReferralProviderService
{
    /// <summary>Code (lazily generated), share link, and lifetime stats.</summary>
    Task<ProviderReferralSummaryResponse> GetSummaryAsync(Guid providerId);

    /// <summary>This provider's own referrals as referrer, newest first.</summary>
    Task<IReadOnlyList<ProviderReferralHistoryItemResponse>> GetHistoryAsync(Guid providerId);
}
