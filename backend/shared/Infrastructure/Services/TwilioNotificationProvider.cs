using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.BuildingBlocks.Privacy;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Real outbound SMS via Twilio's REST API, swapped in for sandbox-simulated
/// SMS once <see cref="TwilioOptions"/> is fully configured - see
/// <see cref="NotificationRegistration"/> for the swap condition.
/// </summary>
/// <remarks>
/// Uses Twilio's plain REST endpoint (HTTP Basic auth, form-encoded body)
/// rather than the official Twilio SDK - no new SDK dependency is needed,
/// only <see cref="IHttpClientFactory"/>, the same choice
/// <see cref="SupabaseFileStorageService"/> made for the same reason.
///
/// Email is not this class's concern: it decorates whichever
/// <see cref="INotificationProvider"/> handles email (real SMTP or sandbox)
/// and delegates <see cref="SendEmailAsync"/> to it unchanged, so Twilio
/// being configured never implies anything about email being configured.
/// </remarks>
public sealed class TwilioNotificationProvider : INotificationProvider
{
    /// <summary>Named <see cref="HttpClient"/> registration - see <see cref="SupabaseFileStorageService.HttpClientName"/> for why named rather than typed.</summary>
    public const string HttpClientName = "Twilio.Sms";

    private readonly INotificationProvider _emailChannel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioNotificationProvider> _logger;

    public TwilioNotificationProvider(
        INotificationProvider emailChannel,
        IHttpClientFactory httpClientFactory,
        IOptions<TwilioOptions> options,
        ILogger<TwilioNotificationProvider> logger)
    {
        _emailChannel = emailChannel;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<Result> SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default) =>
        _emailChannel.SendEmailAsync(toEmail, subject, body, cancellationToken);

    public async Task<Result> SendSmsAsync(string toMobile, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toMobile))
        {
            return Result.Failure(Error.Validation("Notification.InvalidRecipient", "Mobile number is required."));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"Accounts/{_options.AccountSid}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = toMobile,
            ["From"] = _options.FromPhoneNumber!,
            ["Body"] = message,
        });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Status code only, never the response body - same discipline as
            // SupabaseFileStorageService: Twilio's error bodies can echo
            // request details (including the message text) that don't
            // belong in logs.
            _logger.LogError("Twilio SMS send failed with status {StatusCode}.", (int)response.StatusCode);
            return Result.Failure(Error.Business("Notification.SmsSendFailed", "Failed to send the SMS."));
        }

        // Never logs the message body - an OTP code lives in it, same
        // no-secrets-in-logs rule SmtpNotificationProvider follows for email.
        _logger.LogInformation("SMS sent to {MaskedMobile} via Twilio.", ContactMasking.Mask(toMobile));
        return Result.Success();
    }
}
