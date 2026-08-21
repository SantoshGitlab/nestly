using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.BuildingBlocks.Privacy;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Real outbound email via SMTP (Gmail by default - see
/// <see cref="EmailOptions"/>), swapped in for
/// <see cref="SandboxNotificationProvider"/> once <c>Email:AppPassword</c>
/// is configured. SMS is deliberately left at sandbox behaviour: no real SMS
/// vendor exists in this codebase yet (see SandboxNotificationProvider's own
/// doc comment), and email being configured doesn't imply SMS is.
/// </summary>
public class SmtpNotificationProvider : INotificationProvider
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpNotificationProvider> _logger;

    public SmtpNotificationProvider(IOptions<EmailOptions> options, ILogger<SmtpNotificationProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Same simulated behaviour as SandboxNotificationProvider - no real SMS vendor is wired up yet.</summary>
    public Task<Result> SendSmsAsync(string toMobile, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toMobile))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Notification.InvalidRecipient", "Mobile number is required.")));
        }

        _logger.LogInformation("Sandbox SMS simulated for {MaskedMobile}", ContactMasking.Mask(toMobile));
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return Result.Failure(Error.Validation("Notification.InvalidRecipient", "Email address is required."));
        }

        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                Credentials = new NetworkCredential(_options.FromAddress, _options.AppPassword),
                EnableSsl = true,
            };

            using var message = new MailMessage(
                new MailAddress(_options.FromAddress, _options.FromName),
                new MailAddress(toEmail))
            {
                Subject = subject,
                Body = body,
            };

            await client.SendMailAsync(message, cancellationToken);

            // Never logs subject/body - an OTP code lives in the body, same
            // no-secrets-in-logs rule SandboxNotificationProvider follows.
            _logger.LogInformation("Email sent to {MaskedEmail}", ContactMasking.Mask(toEmail));
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {MaskedEmail}", ContactMasking.Mask(toEmail));
            return Result.Failure(Error.Business("Notification.EmailSendFailed", "Failed to send the email."));
        }
    }
}
