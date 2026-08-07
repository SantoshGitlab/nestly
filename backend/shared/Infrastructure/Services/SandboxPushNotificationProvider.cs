using Microsoft.Extensions.Logging;
using Nestly.BuildingBlocks.Privacy;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Sandbox <see cref="IPushNotificationProvider"/> for local/dev
/// environments (task 156): no real FCM/APNs credentials exist in this
/// project, so this simulates delivery and logs only that a send happened,
/// never the device token or message content - same convention as
/// <see cref="SandboxNotificationProvider"/>. A real FCM/APNs integration
/// implements the same interface and is swapped in via <c>AddInfrastructure</c>
/// per environment.
/// </summary>
public class SandboxPushNotificationProvider : IPushNotificationProvider
{
    private readonly ILogger<SandboxPushNotificationProvider> _logger;

    public SandboxPushNotificationProvider(ILogger<SandboxPushNotificationProvider> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Notification.InvalidRecipient", "A device token is required.")));
        }

        _logger.LogInformation("Sandbox push simulated for device {MaskedToken}", ContactMasking.Mask(deviceToken));
        return Task.FromResult(Result.Success());
    }
}
