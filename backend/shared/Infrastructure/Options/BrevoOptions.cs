namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Brevo" configuration section - a
/// transactional email vendor with a genuinely recurring free daily quota
/// (unlike SMS, which has no free-forever tier anywhere), preferred over
/// <see cref="EmailOptions"/>'s Gmail SMTP path when configured. Same
/// optional-with-fallback shape as <see cref="SupabaseStorageOptions"/>.
/// </summary>
/// <remarks>
/// <see cref="ApiKey"/> is a secret and must come from an environment
/// variable (<c>Brevo__ApiKey</c>) or secret store, never a committed
/// appsettings.json - see DEVOPS.md CONFIGURATION AND SECRETS.
/// </remarks>
public class BrevoOptions
{
    public const string SectionName = "Brevo";

    /// <summary>Brevo API v3 key, from Settings -&gt; SMTP &amp; API -&gt; API Keys. Secret - see remarks.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Sender address shown to recipients - must be a verified sender in Brevo.</summary>
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Glavyx";

    /// <summary>
    /// Kill switch. Default true, same convention as
    /// <see cref="SupabaseStorageOptions.Enabled"/>: forces the next email
    /// provider in line even when credentials are present, without deleting
    /// them from the secret store.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Per-send HTTP timeout.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>True when email should go through Brevo: enabled and both the API key and sender address are present.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(FromEmail);
}
