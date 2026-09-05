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
/// </remarks>
public sealed class TwilioNotificationProvider : INotificationProvider
{
    /// <summary>Named <see cref="HttpClient"/> registration - see <see cref="SupabaseFileStorageService.HttpClientName"/> for why named rather than typed.</summary>
    public const string HttpClientName = "Twilio.Sms";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioNotificationProvider> _logger;

    public TwilioNotificationProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<TwilioOptions> options,
        ILogger<TwilioNotificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// This class exists solely for SMS - <see cref="NotificationRegistration"/>
    /// always wraps it in a <see cref="CompositeNotificationProvider"/> that
    /// routes email elsewhere, so this is never actually reached in practice.
    /// Simulates rather than throwing regardless, matching
    /// <see cref="SandboxNotificationProvider"/>'s "never crash on a
    /// vendor gap" posture in case this type is ever resolved standalone.
    /// </summary>
    public Task<Result> SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Notification.InvalidRecipient", "Email address is required.")));
        }

        _logger.LogInformation("Sandbox email simulated for {MaskedEmail}", ContactMasking.Mask(toEmail));
        return Task.FromResult(Result.Success());
    }

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
