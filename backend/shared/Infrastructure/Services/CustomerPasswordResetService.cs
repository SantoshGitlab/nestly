using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Forgot/reset password (SRS 11.2.2). Placed alongside
/// <see cref="CustomerLoginService"/> for the same reason: it needs
/// <see cref="AccountOptions"/>, an Infrastructure-bound config type.
///
/// The flow deliberately verifies against the mobile number rather than the
/// email being reset: mobile ownership was proven by OTP at registration,
/// whereas the email is only ever an unverified identifier at this point, so
/// mailing a reset code to it would let whoever controls that mailbox take
/// over the account.
/// </summary>
public class CustomerPasswordResetService : ICustomerPasswordResetService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAuthIdentityRepository _authIdentityRepository;
    private readonly ICustomerSessionRepository _sessionRepository;
    private readonly IOTPService _otpService;
    private readonly AccountOptions _options;
    private readonly ILogger<CustomerPasswordResetService> _logger;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public CustomerPasswordResetService(
        ICustomerRepository customerRepository,
        ICustomerAuthIdentityRepository authIdentityRepository,
        ICustomerSessionRepository sessionRepository,
        IOTPService otpService,
        IOptions<AccountOptions> options,
        ILogger<CustomerPasswordResetService> logger)
    {
        _customerRepository = customerRepository;
        _authIdentityRepository = authIdentityRepository;
        _sessionRepository = sessionRepository;
        _otpService = otpService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> RequestResetAsync(ForgotPasswordRequest request)
    {
        if (!_options.PasswordAuthEnabled)
        {
            return Result.Failure(Error.Validation("PasswordReset.PasswordAuthDisabled",
                "Password-based authentication is not enabled."));
        }

        var identity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, request.Email);
        if (identity is null)
        {
            // Unknown address: return success without sending anything. An
            // honest 404 here would turn this endpoint into an email-address
            // oracle (SRS 28.3 enumeration). Nothing identifying is logged.
            _logger.LogInformation("Password reset requested for an address with no password identity; no code sent.");
            return Result.Success();
        }

        var customer = await _customerRepository.GetByIdAsync(identity.CustomerId);
        if (customer is null || customer.Status != CustomerStatus.Active)
        {
            _logger.LogInformation("Password reset requested for a customer that is missing or not active; no code sent.");
            return Result.Success();
        }

        var generateResult = await _otpService.GenerateAsync(customer.Mobile, OtpPurpose.PasswordReset);
        if (generateResult.IsFailure)
        {
            // The cooldown/limit errors are safe to surface: they depend on
            // this caller's own recent requests, not on account existence.
            return generateResult;
        }

        return Result.Success();
    }

    public async Task<Result> ResetAsync(ResetPasswordRequest request)
    {
        if (!_options.PasswordAuthEnabled)
        {
            return Result.Failure(Error.Validation("PasswordReset.PasswordAuthDisabled",
                "Password-based authentication is not enabled."));
        }

        // One shared failure for every "this reset cannot proceed" case, so a
        // caller cannot tell an unknown email from a wrong code.
        var invalid = Error.Validation("PasswordReset.Invalid",
            "The reset request is invalid or the code has expired.");

        var identity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, request.Email);
        if (identity is null)
        {
            return Result.Failure(invalid);
        }

        var customer = await _customerRepository.GetByIdAsync(identity.CustomerId);
        if (customer is null || customer.Status != CustomerStatus.Active)
        {
            return Result.Failure(invalid);
        }

        // Purpose-scoped: a login or registration OTP cannot be replayed here.
        var otpResult = await _otpService.ValidateAsync(customer.Mobile, request.OtpCode, OtpPurpose.PasswordReset);
        if (otpResult.IsFailure)
        {
            return otpResult;
        }

        identity.SetPasswordHash(_passwordHasher.HashPassword(customer, request.NewPassword));
        await _authIdentityRepository.UpdateAsync(identity);

        int revoked = await _sessionRepository.RevokeAllForCustomerAsync(customer.Id);
        _logger.LogInformation("Password reset completed for customer {CustomerId}; {RevokedSessionCount} session(s) revoked.",
            customer.Id, revoked);

        return Result.Success();
    }
}
