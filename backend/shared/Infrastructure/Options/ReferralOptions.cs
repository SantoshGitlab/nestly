namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Referral" configuration section (REFERRAL.md,
/// task 162). Only the share-link base URL lives here rather than in a DB
/// row: it depends on which public customer-web domain is live, and per
/// docs/DEVOPS.md OPEN DECISIONS the hosting domain itself isn't decided
/// yet - same "not-yet-adminable, options-file for now" reasoning as
/// <see cref="CommissionOptions"/>.
/// </summary>
public class ReferralOptions
{
    public const string SectionName = "Referral";

    /// <summary>Customer-web registration URL a referral code is appended to as a query parameter, e.g. "https://nestly.app/register?ref=".</summary>
    public string ShareLinkBaseUrl { get; set; } = "https://nestly.app/register?ref=";
}
