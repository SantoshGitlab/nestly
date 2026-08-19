using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Real <see cref="IPushNotificationProvider"/> backed by Firebase Cloud
/// Messaging via the official <c>FirebaseAdmin</c> SDK (task 307's
/// server-side counterpart to <c>frontend/*-web</c>'s <c>lib/push.ts</c>).
/// </summary>
/// <remarks>
/// <b>This class does not throw.</b> Every failure path narrows to a
/// <see cref="Result.Failure(Error)"/> and a log line - see
/// <see cref="IPushNotificationProvider"/>'s own contract and
/// <see cref="SandboxPushNotificationProvider"/>'s identical convention.
/// The most common failure, a stale token from an uninstalled app or a
/// revoked notification permission, is routine and logged at
/// <see cref="LogLevel.Information"/> rather than as a warning - the caller
/// (<c>NotificationDispatchService</c>) already treats one channel failing
/// as non-fatal, so this must not read as an incident on every dashboard
/// scan.
/// </remarks>
public sealed class FirebasePushNotificationProvider : IPushNotificationProvider
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushNotificationProvider> _logger;

    public FirebasePushNotificationProvider(FirebaseApp app, ILogger<FirebasePushNotificationProvider> logger)
    {
        _messaging = FirebaseMessaging.GetMessaging(app);
        _logger = logger;
    }

    public async Task<Result> SendPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return Result.Failure(Error.Validation("Notification.InvalidRecipient", "A device token is required."));
        }

        // Message.Token, not the newer Firebase Installation ID (Fid): this
        // whole system - IDeviceTokenService, the DeviceToken entity, and
        // both frontend apps' lib/push.ts (getToken(), not the FID-based
        // register()/onRegistered() flow) - is built end-to-end around the
        // registration-token model. Fid is a separate addressing scheme with
        // its own frontend registration flow and storage shape; adopting it
        // would be a real migration, not a substitution, and out of scope
        // here. Token is deprecated, not removed, and still fully supported.
#pragma warning disable CS0618
        var message = new Message
        {
            Token = deviceToken,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
        };
#pragma warning restore CS0618

        try
        {
            // Never logged: deviceToken (a per-device secret in its own
            // right) and the message content - same key-hygiene rule
            // GoogleMapsRouteEstimateProvider documents for its API key.
            await _messaging.SendAsync(message, cancellationToken);
            return Result.Success();
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogInformation(
                "Push send failed with FCM error code {ErrorCode} ({MessagingErrorCode}).",
                ex.ErrorCode, ex.MessagingErrorCode);
            return Result.Failure(Error.Infrastructure("Notification.PushSendFailed", "The push notification could not be delivered."));
        }
    }
}
