using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// <see cref="IPushNotificationProvider"/> registration (task 307's
/// server-side wiring). Its own file, same reasoning as
/// <see cref="RouteEstimateRegistration"/>: the implementation is chosen by
/// configuration rather than fixed, and that choice deserves to be readable
/// on its own.
/// </summary>
internal static class PushNotificationRegistration
{
    /// <summary>
    /// Registers Firebase Cloud Messaging when a service account key is
    /// configured and the sandbox provider otherwise.
    /// </summary>
    internal static IServiceCollection AddPushNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Nothing a process cannot start without - the key path is optional
        // by design - so no ValidateOnStart, same reasoning as
        // RouteEstimateRegistration's GoogleMapsOptions binding.
        services
            .AddOptions<FirebaseOptions>()
            .Bind(configuration.GetSection(FirebaseOptions.SectionName));

        services.AddSingleton<SandboxPushNotificationProvider>();

        services.AddSingleton<IPushNotificationProvider>(serviceProvider =>
        {
            var firebaseOptions = serviceProvider.GetRequiredService<IOptions<FirebaseOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PushNotificationRegistration));

            if (!firebaseOptions.IsConfigured)
            {
                // Logged at startup so "why did push notifications not
                // arrive?" is answerable from the logs without reading
                // configuration - same convention as RouteEstimateRegistration.
                logger.LogInformation(
                    "Push notifications will use the sandbox provider: Firebase is {State}.",
                    firebaseOptions.Enabled ? "missing a service account key path" : "disabled by configuration");

                return serviceProvider.GetRequiredService<SandboxPushNotificationProvider>();
            }

            if (!File.Exists(firebaseOptions.ServiceAccountKeyPath))
            {
                logger.LogWarning(
                    "Push notifications will use the sandbox provider: the configured Firebase service account key file does not exist.");
                return serviceProvider.GetRequiredService<SandboxPushNotificationProvider>();
            }

            try
            {
                // CredentialFactory.FromFile<T>, not the simpler
                // GoogleCredential.FromFile/FromStream: both of those are
                // obsolete in this SDK version (potential security risk per
                // their own deprecation message), and this is the documented
                // replacement - load the specific credential type, then
                // convert it.
                var serviceAccountCredential = CredentialFactory.FromFile<ServiceAccountCredential>(firebaseOptions.ServiceAccountKeyPath);

                // DefaultInstance first: FirebaseApp.Create() throws if a
                // default app already exists in this process, which happens
                // in-process across repeated test-host spins (WebApplicationFactory)
                // even though it never happens for a single real deployment.
                var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
                {
                    Credential = serviceAccountCredential.ToGoogleCredential(),
                });

                logger.LogInformation("Push notifications will use Firebase Cloud Messaging.");
                return ActivatorUtilities.CreateInstance<FirebasePushNotificationProvider>(serviceProvider, app);
            }
            catch (Exception ex)
            {
                // A malformed key file, revoked credentials, etc. - degrade
                // to sandbox rather than fail process startup over a
                // third-party integration nothing else in the system depends
                // on (same posture GoogleMapsOptions/RouteEstimateRegistration
                // takes).
                logger.LogWarning(ex, "Push notifications will use the sandbox provider: Firebase initialization failed.");
                return serviceProvider.GetRequiredService<SandboxPushNotificationProvider>();
            }
        });

        return services;
    }
}
