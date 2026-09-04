namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Email" configuration section: SMTP
/// settings for real outbound email (OTP codes, welcome messages, etc.),
/// swapped in for <c>SandboxNotificationProvider</c> once configured - see
/// <c>DependencyInjection.AddInfrastructure</c> for the swap condition.
///
/// <see cref="AppPassword"/> is a real secret and is deliberately left blank
/// here and in every checked-in appsettings*.json - set it locally via
/// `dotnet user-secrets set "Email:AppPassword" "..."` (run from the API
/// project directory) or the `Email__AppPassword` environment variable in
/// production/docker-compose. Never commit a real value for it.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    /// <summary>Sender address shown to recipients, and the SMTP auth username for Gmail (the two are the same account for Gmail SMTP).</summary>
    public string FromAddress { get; set; } = "Nestly.test123@gmail.com";

    public string FromName { get; set; } = "Glavyx";

    /// <summary>
    /// Gmail's 16-character App Password (Google Account -&gt; Security -&gt;
    /// 2-Step Verification -&gt; App passwords) - not the account's login
    /// password, which Gmail's SMTP rejects outright once 2-Step
    /// Verification is on. Empty means email sending stays on the sandbox
    /// provider (no real send attempted).
    /// </summary>
    public string AppPassword { get; set; } = string.Empty;
}
