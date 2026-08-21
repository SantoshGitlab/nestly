using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Nestly.BuildingBlocks.Privacy;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Real outbound email via SMTP (Gmail by default - see
/// <see cref="EmailOptions"/>), swapped in for
/// <see cref="SandboxNotificationProvider"/> once <c>Email:AppPassword</c>
/// is configured. Uses MailKit rather than the built-in
/// <c>System.Net.Mail.SmtpClient</c> - Microsoft's own docs mark that class
/// "not recommended for new development", and it fails Gmail's STARTTLS/AUTH
/// sequence outright ("5.7.0 Authentication Required") even with a verified
/// correct App Password and 2-Step Verification on; MailKit is the standard
/// replacement and negotiates Gmail's SMTP correctly. SMS is deliberately
/// left at sandbox behaviour: no real SMS vendor exists in this codebase yet
/// (see SandboxNotificationProvider's own doc comment), and email being
/// configured doesn't imply SMS is.
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
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient
            {
                // The certificate chain and hostname are still fully
                // validated - this only skips the live OCSP revocation-status
                // check, which some sandboxed/restricted networks cannot
                // complete (no outbound path to Google's OCSP responder),
                // causing .NET's TLS stack to fail the handshake entirely
                // even though the certificate itself is genuinely valid.
                CheckCertificateRevocation = false,
            };
            // Gmail's port 587 starts in plaintext and upgrades via STARTTLS -
            // SecureSocketOptions.StartTls forces that negotiation rather than
            // attempting an immediate TLS handshake (which is port 465's
            // convention, not 587's), the mismatch that trips up
            // System.Net.Mail.SmtpClient's default behaviour on this exact port.
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
            // Google displays an App Password as four space-separated groups
            // ("xxxx xxxx xxxx xxxx") for readability; the real 16-character
            // credential has no spaces, and Gmail's SMTP AUTH rejects the
            // display form verbatim ("535 5.7.8 BadCredentials") even though
            // it looks identical to a human. Stripping whitespace here means
            // the config value can be pasted exactly as Google shows it.
            string appPassword = _options.AppPassword.Replace(" ", string.Empty);
            await client.AuthenticateAsync(_options.FromAddress, appPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

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
