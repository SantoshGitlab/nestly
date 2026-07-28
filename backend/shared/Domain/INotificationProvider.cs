using Nestly.BuildingBlocks.Results;

namespace Nestly.Domain;

public enum NotificationChannel
{
    Sms,
    Email
}

/// <summary>
/// Communication provider abstraction (SRS 30.2): the customer-facing OTP,
/// registration, and account flows send through this interface rather than
/// binding to a specific SMS/email vendor, so the vendor can be swapped
/// (e.g. sandbox in dev, a real gateway in production) via DI only.
/// </summary>
public interface INotificationProvider
{
    Task<Result> SendSmsAsync(string toMobile, string message, CancellationToken cancellationToken = default);

    Task<Result> SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
