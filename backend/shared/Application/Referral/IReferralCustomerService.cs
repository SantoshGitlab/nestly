namespace Nestly.Application.Referral;

/// <summary>Backs the consumer-api's Refer &amp; Earn screen (task 168).</summary>
public interface IReferralCustomerService
{
    /// <summary>Code (lazily generated), share link, and lifetime stats.</summary>
    Task<ReferralSummaryResponse> GetSummaryAsync(Guid customerId);

    /// <summary>This customer's own referrals as referrer, newest first.</summary>
    Task<IReadOnlyList<ReferralHistoryItemResponse>> GetHistoryAsync(Guid customerId);
}
