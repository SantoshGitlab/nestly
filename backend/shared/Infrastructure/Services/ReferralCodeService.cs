using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Referral;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IReferralCodeService"/>.</summary>
public class ReferralCodeService : IReferralCodeService
{
    // Excludes 0/O and 1/I/L - visually ambiguous in a shared link or typed
    // into a registration form (REFERRAL.md task 162 "shareable link format").
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeLength = 8;
    private const int MaxGenerationAttempts = 10;

    private readonly ICustomerRepository _customerRepository;
    private readonly ReferralOptions _options;

    public ReferralCodeService(ICustomerRepository customerRepository, IOptions<ReferralOptions> options)
    {
        _customerRepository = customerRepository;
        _options = options.Value;
    }

    public async Task<string> GetOrCreateCodeAsync(Guid customerId)
    {
        Customer? customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException($"Customer {customerId} does not exist.");

        if (customer.ReferralCode is not null)
        {
            return customer.ReferralCode;
        }

        string code = await GenerateUniqueCodeAsync();
        customer.SetReferralCode(code);
        await _customerRepository.UpdateAsync(customer);
        return code;
    }

    public string BuildShareLink(string referralCode) => $"{_options.ShareLinkBaseUrl}{referralCode}";

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            string candidate = GenerateCandidate();
            if (!await _customerRepository.ExistsByReferralCodeAsync(candidate))
            {
                return candidate;
            }
        }

        // At 31^8 possible codes, exhausting MaxGenerationAttempts on
        // collisions alone is not a realistic outcome at any customer count
        // this platform will reach - if it ever happens, that is itself a
        // signal something is wrong (e.g. a broken RNG), worth failing loudly
        // on rather than silently degrading to a shorter/weaker code.
        throw new InvalidOperationException(
            $"Could not generate a unique referral code after {MaxGenerationAttempts} attempts.");
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
