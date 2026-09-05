namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Twilio" configuration section - the real
/// SMS vendor for OTP and account notifications, replacing the sandbox
/// simulation (SRS 30.2). Same optional-with-fallback shape as
/// <see cref="SupabaseStorageOptions"/>: absent credentials mean SMS keeps
/// simulating rather than failing the process at startup.
/// </summary>
/// <remarks>
/// <see cref="AuthToken"/> is a secret and must come from an environment
/// variable (<c>Twilio__AuthToken</c>) or secret store, never a committed
/// appsettings.json - see DEVOPS.md CONFIGURATION AND SECRETS.
/// </remarks>
public class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>Twilio Account SID, e.g. "ACxxxxxxxx...". Not a secret on its own, but conventionally kept alongside the auth token.</summary>
    public string? AccountSid { get; set; }

    /// <summary>Twilio Auth Token. Secret - see remarks.</summary>
    public string? AuthToken { get; set; }

    /// <summary>The Twilio phone number messages are sent from, in E.164 format (e.g. "+15005550006") - a trial account's free number works here unchanged.</summary>
    public string? FromPhoneNumber { get; set; }

    /// <summary>
    /// Kill switch. Default true, same convention as
    /// <see cref="SupabaseStorageOptions.Enabled"/>: forces the sandbox
    /// fallback even when credentials are present, without deleting them
    /// from the secret store.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True when SMS should go through Twilio: enabled and every credential is present. Mirrors <see cref="SupabaseStorageOptions.IsConfigured"/>.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(FromPhoneNumber);
}
