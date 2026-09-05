using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// <see cref="INotificationProvider"/> registration (SRS 30.2). Own file,
/// same reasoning as <see cref="PushNotificationRegistration"/>: email and
/// SMS are each chosen independently by configuration rather than fixed, and
/// that choice deserves to be readable on its own rather than growing inline
/// in <c>DependencyInjection.AddInfrastructure</c>.
/// </summary>
internal static class NotificationRegistration
{
    /// <summary>
    /// Registers real Gmail SMTP email once <c>Email:AppPassword</c> is set
    /// and real Twilio SMS once every <see cref="TwilioOptions"/> credential
    /// is set - independently of each other, since one channel being real
    /// says nothing about the other. Either or both fall back to the sandbox
    /// provider's simulated behaviour when unconfigured.
    /// </summary>
    internal static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        // Reading the raw config value here rather than IOptions<T> because
        // this decision runs at registration time, before the container that
        // would resolve IOptions<T> exists yet - same reasoning the previous
        // inline version of this check used.
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        // Neither section holds anything a process cannot start without -
        // both are optional by design - so no ValidateOnStart, same
        // reasoning as SupabaseStorageOptions/FirebaseOptions.
        services
            .AddOptions<TwilioOptions>()
            .Bind(configuration.GetSection(TwilioOptions.SectionName));

        services.AddHttpClient(TwilioNotificationProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/2010-04-01/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<SandboxNotificationProvider>();
        services.AddScoped<SmtpNotificationProvider>();
        services.AddScoped<TwilioNotificationProvider>();

        services.AddScoped<INotificationProvider>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NotificationRegistration));

            var emailConfigured = !string.IsNullOrWhiteSpace(
                configuration[$"{EmailOptions.SectionName}:{nameof(EmailOptions.AppPassword)}"]);

            // Logged at startup so "why didn't the email/SMS arrive?" is
            // answerable from the logs without reading configuration - same
            // convention as PushNotificationRegistration/RouteEstimateRegistration.
            INotificationProvider emailChannel;
            if (emailConfigured)
            {
                logger.LogInformation("Email notifications will use real Gmail SMTP delivery.");
                emailChannel = serviceProvider.GetRequiredService<SmtpNotificationProvider>();
            }
            else
            {
                logger.LogInformation("Email notifications will use the sandbox provider: Email:AppPassword is not configured.");
                emailChannel = serviceProvider.GetRequiredService<SandboxNotificationProvider>();
            }

            var twilioOptions = serviceProvider.GetRequiredService<IOptions<TwilioOptions>>().Value;
            if (!twilioOptions.IsConfigured)
            {
                logger.LogInformation(
                    "SMS notifications will use the sandbox provider: Twilio is {State}.",
                    twilioOptions.Enabled ? "missing an account SID, auth token, or sender number" : "disabled by configuration");

                // No real SMS vendor - the email-channel provider already
                // implements the full interface (its own SendSmsAsync is the
                // same sandbox simulation), so it can stand in directly.
                return emailChannel;
            }

            logger.LogInformation("SMS notifications will use Twilio.");
            return ActivatorUtilities.CreateInstance<TwilioNotificationProvider>(serviceProvider, emailChannel);
        });

        return services;
    }
}
