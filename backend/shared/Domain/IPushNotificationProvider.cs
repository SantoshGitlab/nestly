using Nestly.BuildingBlocks.Results;

namespace Nestly.Domain;

/// <summary>
/// Push provider abstraction (SRS 19.1, task 156). Kept separate from
/// <see cref="INotificationProvider"/> rather than adding a third method to
/// it: push addresses a device token, not a mobile number or email address,
/// and a customer may have several registered devices - the dispatch shape
/// (fan out per token, not a single send) is different enough to warrant
/// its own interface, matching how this codebase already keeps OTP's
/// Sms/Email interface separate from the payment gateway's.
/// </summary>
public interface IPushNotificationProvider
{
    Task<Result> SendPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default);
}
