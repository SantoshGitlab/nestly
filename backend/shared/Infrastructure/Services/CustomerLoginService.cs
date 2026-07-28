using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Login, session issuance, refresh, and logout (SRS 11.2.2). Same placement
/// rationale as <see cref="CustomerRegistrationService"/>.
/// </summary>
public class CustomerLoginService : ICustomerLoginService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAuthIdentityRepository _authIdentityRepository;
    private readonly ICustomerSessionRepository _sessionRepository;
    private readonly ILoginAttemptRepository _loginAttemptRepository;
    private readonly IOTPService _otpService;
    private readonly ITokenService _tokenService;
    private readonly AccountOptions _accountOptions;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public CustomerLoginService(
        ICustomerRepository customerRepository,
        ICustomerAuthIdentityRepository authIdentityRepository,
        ICustomerSessionRepository sessionRepository,
        ILoginAttemptRepository loginAttemptRepository,
        IOTPService otpService,
        ITokenService tokenService,
        IOptions<AccountOptions> accountOptions)
    {
        _customerRepository = customerRepository;
        _authIdentityRepository = authIdentityRepository;
        _sessionRepository = sessionRepository;
        _loginAttemptRepository = loginAttemptRepository;
        _otpService = otpService;
        _tokenService = tokenService;
        _accountOptions = accountOptions.Value;
    }

    public async Task<Result> RequestOtpAsync(RequestLoginOtpRequest request)
    {
        var customer = await _customerRepository.GetByMobileAsync(request.Mobile);
        if (customer is null)
        {
            // Same message either way avoids confirming/denying whether a
            // mobile number is registered (SRS 28.1/28.3 enumeration risk).
            return Result.Failure(Error.NotFound("Login.NotFound", "No account found for this mobile number."));
        }

        return await _otpService.GenerateAsync(request.Mobile, OtpPurpose.Login);
    }

    public async Task<Result<LoginResponse>> LoginWithOtpAsync(LoginWithOtpRequest request)
    {
        var lockout = await CheckLockoutAsync(request.Mobile);
        if (lockout is not null)
        {
            return Result.Failure<LoginResponse>(lockout);
        }

        var otpResult = await _otpService.ValidateAsync(request.Mobile, request.OtpCode, OtpPurpose.Login);
        if (otpResult.IsFailure)
        {
            await RecordAttemptAsync(request.Mobile, succeeded: false);
            return Result.Failure<LoginResponse>(otpResult.Error);
        }

        var customer = await _customerRepository.GetByMobileAsync(request.Mobile);
        if (customer is null)
        {
            await RecordAttemptAsync(request.Mobile, succeeded: false);
            return Result.Failure<LoginResponse>(Error.NotFound("Login.NotFound", "No account found for this mobile number."));
        }

        await RecordAttemptAsync(request.Mobile, succeeded: true);
        return await IssueSessionAsync(customer);
    }

    public async Task<Result<LoginResponse>> LoginWithPasswordAsync(LoginWithPasswordRequest request)
    {
        var lockout = await CheckLockoutAsync(request.Email);
        if (lockout is not null)
        {
            return Result.Failure<LoginResponse>(lockout);
        }

        var identity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, request.Email);
        if (identity is null || identity.PasswordHash is null)
        {
            await RecordAttemptAsync(request.Email, succeeded: false);
            return Result.Failure<LoginResponse>(Error.Unauthorized("Login.InvalidCredentials", "Invalid email or password."));
        }

        var customer = await _customerRepository.GetByIdAsync(identity.CustomerId);
        if (customer is null)
        {
            await RecordAttemptAsync(request.Email, succeeded: false);
            return Result.Failure<LoginResponse>(Error.Unauthorized("Login.InvalidCredentials", "Invalid email or password."));
        }

        var verification = _passwordHasher.VerifyHashedPassword(customer, identity.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordAttemptAsync(request.Email, succeeded: false);
            return Result.Failure<LoginResponse>(Error.Unauthorized("Login.InvalidCredentials", "Invalid email or password."));
        }

        await RecordAttemptAsync(request.Email, succeeded: true);
        return await IssueSessionAsync(customer);
    }

    public async Task<Result<LoginResponse>> RefreshAsync(RefreshTokenRequest request)
    {
        var session = await _sessionRepository.GetByRefreshTokenHashAsync(Hash(request.RefreshToken));
        if (session is null || !session.IsActive(DateTime.UtcNow))
        {
            return Result.Failure<LoginResponse>(Error.Unauthorized("Login.InvalidRefreshToken", "The refresh token is invalid or has expired."));
        }

        var customer = await _customerRepository.GetByIdAsync(session.CustomerId);
        if (customer is null)
        {
            return Result.Failure<LoginResponse>(Error.Unauthorized("Login.InvalidRefreshToken", "The refresh token is invalid or has expired."));
        }

        // Rotate on every use: the old refresh token is revoked immediately so
        // it cannot be replayed if it was intercepted (SRS 28.3).
        session.Revoke();
        await _sessionRepository.UpdateAsync(session);

        return await IssueSessionAsync(customer, session.DeviceInfo, session.IpAddress);
    }

    public async Task<Result> LogoutAsync(LogoutRequest request)
    {
        var session = await _sessionRepository.GetByRefreshTokenHashAsync(Hash(request.RefreshToken));
        if (session is null)
        {
            // Logging out an already-invalid token is not an error from the
            // caller's point of view: the end state (no active session) holds.
            return Result.Success();
        }

        session.Revoke();
        await _sessionRepository.UpdateAsync(session);
        return Result.Success();
    }

    private async Task<Result<LoginResponse>> IssueSessionAsync(Customer customer, string? deviceInfo = null, string? ipAddress = null)
    {
        if (customer.Status != CustomerStatus.Active)
        {
            return Result.Failure<LoginResponse>(Error.Forbidden("Login.AccountNotActive", "This account cannot log in."));
        }

        var accessToken = _tokenService.GenerateAccessToken(customer.Id, customer.Mobile);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var now = DateTime.UtcNow;

        var session = new CustomerSession(
            Guid.NewGuid(), customer.Id, Hash(refreshToken), now, now.Add(_tokenService.RefreshTokenLifetime),
            deviceInfo, ipAddress);
        await _sessionRepository.AddAsync(session);

        return Result.Success(new LoginResponse(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken));
    }

    /// <summary>
    /// A new OTP resets <see cref="CustomerOtp.AttemptCount"/>, so that alone
    /// cannot stop repeated bursts of wrong guesses across multiple OTPs or
    /// password tries. This checks failures recorded independently of any
    /// single OTP/password check, over a rolling window (SRS 11.2.2, 28.1).
    /// </summary>
    private async Task<Error?> CheckLockoutAsync(string identifier)
    {
        var since = DateTime.UtcNow.AddMinutes(-_accountOptions.LockoutWindowMinutes);
        int failures = await _loginAttemptRepository.CountFailuresSinceAsync(identifier, since);
        if (failures >= _accountOptions.MaxFailedLoginAttempts)
        {
            return Error.Forbidden("Login.AccountLocked",
                $"Too many failed attempts. Try again in {_accountOptions.LockoutWindowMinutes} minutes.");
        }

        return null;
    }

    private Task RecordAttemptAsync(string identifier, bool succeeded) =>
        _loginAttemptRepository.AddAsync(new LoginAttempt(Guid.NewGuid(), identifier, succeeded, DateTime.UtcNow));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
