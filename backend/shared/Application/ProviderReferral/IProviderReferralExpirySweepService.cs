namespace Nestly.Application.ProviderReferral;

/// <summary>Scheduled sweep (ProviderReferralExpirySweepJob): marks Registered provider referrals whose ExpiresAtUtc has passed as Expired.</summary>
public interface IProviderReferralExpirySweepService
{
    Task SweepAsync(CancellationToken cancellationToken = default);
}
