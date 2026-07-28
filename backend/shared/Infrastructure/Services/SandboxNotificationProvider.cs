using Microsoft.Extensions.Logging;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Sandbox <see cref="INotificationProvider"/> for local/dev environments: it
/// never calls a real SMS/email vendor and never logs message content (which
/// may carry an OTP), only that a send was simulated — matching the
/// no-PII/no-secrets logging rule the OTP service itself already follows.
/// A real vendor integration implements the same interface and is swapped in
/// via <c>AddInfrastructure</c> per environment.
/// </summary>
public class SandboxNotificationProvider : INotificationProvider
{
    private readonly ILogger<SandboxNotificationProvider> _logger;

    public SandboxNotificationProvider(ILogger<SandboxNotificationProvider> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendSmsAsync(string toMobile, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toMobile))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Notification.InvalidRecipient", "Mobile number is required.")));
        }

        _logger.LogInformation("Sandbox SMS simulated for {MaskedMobile}", Mask(toMobile));
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Notification.InvalidRecipient", "Email address is required.")));
        }

        _logger.LogInformation("Sandbox email simulated for {MaskedEmail}", Mask(toEmail));
        return Task.FromResult(Result.Success());
    }

    private static string Mask(string value)
    {
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - 4) + value[^4..];
    }
}
