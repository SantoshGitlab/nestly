using Microsoft.Extensions.Logging;
using Nestly.Application.ProviderReferral;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderReferralExpirySweepService"/>.</summary>
public class ProviderReferralExpirySweepService : IProviderReferralExpirySweepService
{
    private readonly IProviderReferralRepository _referralRepository;
    private readonly ILogger<ProviderReferralExpirySweepService> _logger;

    public ProviderReferralExpirySweepService(
        IProviderReferralRepository referralRepository,
        ILogger<ProviderReferralExpirySweepService> logger)
    {
        _referralRepository = referralRepository;
        _logger = logger;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _referralRepository.ListExpiredAsync(DateTime.UtcNow);
        foreach (var referral in expired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            referral.MarkExpired();
            await _referralRepository.UpdateAsync(referral);
        }

        if (expired.Count > 0)
        {
            _logger.LogInformation("Provider referral expiry sweep closed {Count} unqualified referral(s).", expired.Count);
        }
    }
}
