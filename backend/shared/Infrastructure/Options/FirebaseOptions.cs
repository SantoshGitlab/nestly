namespace Nestly.Infrastructure.Options;

/// <summary>
/// Firebase Cloud Messaging server-side config (task 307's real-provider
/// counterpart to frontend/*-web's lib/push.ts and firebase-messaging-sw.js).
/// </summary>
public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// Absolute path to the Firebase service-account JSON key file (Firebase
    /// Console &gt; Project settings &gt; Service accounts &gt; Generate new
    /// private key). When absent, push falls back to
    /// <see cref="Nestly.Infrastructure.Services.SandboxPushNotificationProvider"/> - same
    /// "optional third-party integration" convention as
    /// <see cref="GoogleMapsOptions.ApiKey"/>.
    ///
    /// Deliberately a file path, not the JSON content inline: that content is
    /// a full admin credential for the Firebase project (it can send push to
    /// every registered device) and must never sit in appsettings.json, any
    /// other file this repo tracks, or source control at all. Set via
    /// `dotnet user-secrets` for local dev, or the production secret
    /// store/env var for a real deployment - the path itself points
    /// somewhere outside the repo entirely.
    /// </summary>
    public string? ServiceAccountKeyPath { get; set; }

    /// <summary>Kill switch. Same convention as <see cref="GoogleMapsOptions.Enabled"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// True when real push should be used: the integration is switched on and
    /// a key path is configured. Mirrors <see cref="GoogleMapsOptions.IsConfigured"/>.
    /// </summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ServiceAccountKeyPath);
}
