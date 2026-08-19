using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.ProviderIdentity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Forgot/reset password for providers (task 372), structurally mirroring
/// <see cref="CustomerPasswordResetService"/>.
///
/// The flow deliberately verifies against the mobile number rather than the
/// email being reset: mobile ownership was proven by OTP at registration,
/// whereas the email is only ever an unverified identifier at this point, so
/// mailing a reset code to it would let whoever controls that mailbox take
/// over the account.
/// </summary>
public class ProviderPasswordResetService : IProviderPasswordResetService
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderAuthIdentityRepository _authIdentityRepository;
    private readonly IProviderSessionRepository _sessionRepository;
    private readonly IProviderOtpService _otpService;
    private readonly ProviderAccountOptions _options;
    private readonly ILogger<ProviderPasswordResetService> _logger;
    private readonly PasswordHasher<Provider> _passwordHasher = new();

    public ProviderPasswordResetService(
        IProviderRepository providerRepository,
        IProviderAuthIdentityRepository authIdentityRepository,
        IProviderSessionRepository sessionRepository,
        IProviderOtpService otpService,
        IOptions<ProviderAccountOptions> options,
        ILogger<ProviderPasswordResetService> logger)
    {
        _providerRepository = providerRepository;
        _authIdentityRepository = authIdentityRepository;
        _sessionRepository = sessionRepository;
        _otpService = otpService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> RequestResetAsync(ForgotProviderPasswordRequest request)
    {
        if (!_options.PasswordAuthEnabled)
        {
            return Result.Failure(Error.Validation("ProviderPasswordReset.PasswordAuthDisabled",
                "Password-based authentication is not enabled."));
        }

        var identity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, request.Email);
        if (identity is null)
        {
            // Unknown address: return success without sending anything. An
            // honest 404 here would turn this endpoint into an email-address
            // oracle (mirrors CustomerPasswordResetService). Nothing
            // identifying is logged.
            _logger.LogInformation("Provider password reset requested for an address with no password identity; no code sent.");
            return Result.Success();
        }

        var provider = await _providerRepository.GetByIdAsync(identity.ProviderId);
        if (provider is null || provider.Status is ProviderStatus.Suspended or ProviderStatus.Deactivated)
        {
            _logger.LogInformation("Provider password reset requested for a provider that is missing or not active; no code sent.");
            return Result.Success();
        }

        var generateResult = await _otpService.GenerateAsync(provider.Phone, OtpPurpose.PasswordReset);
        if (generateResult.IsFailure)
        {
            // The cooldown/limit errors are safe to surface: they depend on
            // this caller's own recent requests, not on account existence.
            return generateResult;
        }

        return Result.Success();
    }

    public async Task<Result> ResetAsync(ResetProviderPasswordRequest request)
    {
        if (!_options.PasswordAuthEnabled)
        {
            return Result.Failure(Error.Validation("ProviderPasswordReset.PasswordAuthDisabled",
                "Password-based authentication is not enabled."));
        }

        // One shared failure for every "this reset cannot proceed" case, so a
        // caller cannot tell an unknown email from a wrong code.
        var invalid = Error.Validation("ProviderPasswordReset.Invalid",
            "The reset request is invalid or the code has expired.");

        var identity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, request.Email);
        if (identity is null)
        {
            return Result.Failure(invalid);
        }

        var provider = await _providerRepository.GetByIdAsync(identity.ProviderId);
        if (provider is null || provider.Status is ProviderStatus.Suspended or ProviderStatus.Deactivated)
        {
            return Result.Failure(invalid);
        }

        // Purpose-scoped: a login or registration OTP cannot be replayed here.
        var otpResult = await _otpService.ValidateAsync(provider.Phone, request.OtpCode, OtpPurpose.PasswordReset);
        if (otpResult.IsFailure)
        {
            return otpResult;
        }

        identity.SetPasswordHash(_passwordHasher.HashPassword(provider, request.NewPassword));
        await _authIdentityRepository.UpdateAsync(identity);

        int revoked = await _sessionRepository.RevokeAllForProviderAsync(provider.Id);
        _logger.LogInformation("Password reset completed for provider {ProviderId}; {RevokedSessionCount} session(s) revoked.",
            provider.Id, revoked);

        return Result.Success();
    }
}
