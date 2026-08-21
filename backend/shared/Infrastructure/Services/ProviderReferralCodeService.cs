using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.ProviderReferral;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderReferralCodeService"/>. Mirrors ReferralCodeService.</summary>
public class ProviderReferralCodeService : IProviderReferralCodeService
{
    // Excludes 0/O and 1/I/L - visually ambiguous in a shared link or typed
    // into a registration form (mirrors ReferralCodeService.Alphabet).
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeLength = 8;
    private const int MaxGenerationAttempts = 10;

    private readonly IProviderRepository _providerRepository;
    private readonly ProviderReferralOptions _options;

    public ProviderReferralCodeService(IProviderRepository providerRepository, IOptions<ProviderReferralOptions> options)
    {
        _providerRepository = providerRepository;
        _options = options.Value;
    }

    public async Task<string> GetOrCreateCodeAsync(Guid providerId)
    {
        Domain.Provider? provider = await _providerRepository.GetByIdAsync(providerId)
            ?? throw new InvalidOperationException($"Provider {providerId} does not exist.");

        if (provider.ReferralCode is not null)
        {
            return provider.ReferralCode;
        }

        string code = await GenerateUniqueCodeAsync();
        provider.SetReferralCode(code);
        await _providerRepository.UpdateAsync(provider);
        return code;
    }

    public string BuildShareLink(string referralCode) => $"{_options.ShareLinkBaseUrl}{referralCode}";

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            string candidate = GenerateCandidate();
            if (!await _providerRepository.ExistsByReferralCodeAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a unique provider referral code after {MaxGenerationAttempts} attempts.");
    }

    private static string GenerateCandidate()
    {
        var builder = new StringBuilder(CodeLength);
        for (int i = 0; i < CodeLength; i++)
        {
            builder.Append(Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)]);
        }

        return builder.ToString();
    }
}
