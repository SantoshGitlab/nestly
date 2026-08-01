using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.PartnerIdentity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Partner OTP login, session issuance, refresh, and logout (task 146b),
/// structurally mirroring <see cref="CustomerLoginService"/>. No password
/// login path - PARTNER.md's API surface lists only OTP for partners.
///
/// Unlike <see cref="CustomerLoginService.IssueSessionAsync"/> (which only
/// allows <c>CustomerStatus.Active</c>), a session is issued for both
/// <see cref="PartnerStatus.PendingVerification"/> and
/// <see cref="PartnerStatus.Active"/>: a newly registered partner has not
/// been through admin KYC approval yet, but still needs to log back in to
/// finish onboarding (upload KYC documents, complete profile). Only
/// <see cref="PartnerStatus.Suspended"/>/<see cref="PartnerStatus.Deactivated"/>
/// are blocked.
/// </summary>
public class PartnerLoginService : IPartnerLoginService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerSessionRepository _sessionRepository;
    private readonly IPartnerLoginAttemptRepository _loginAttemptRepository;
    private readonly IPartnerOtpService _otpService;
    private readonly IPartnerTokenService _tokenService;
    private readonly PartnerAccountOptions _accountOptions;

    public PartnerLoginService(
        IPartnerRepository partnerRepository,
        IPartnerSessionRepository sessionRepository,
        IPartnerLoginAttemptRepository loginAttemptRepository,
        IPartnerOtpService otpService,
        IPartnerTokenService tokenService,
        IOptions<PartnerAccountOptions> accountOptions)
    {
        _partnerRepository = partnerRepository;
        _sessionRepository = sessionRepository;
        _loginAttemptRepository = loginAttemptRepository;
        _otpService = otpService;
        _tokenService = tokenService;
        _accountOptions = accountOptions.Value;
    }

    public async Task<Result> RequestOtpAsync(RequestPartnerLoginOtpRequest request)
    {
        var partner = await _partnerRepository.GetByPhoneAsync(request.Mobile);
        if (partner is null)
        {
            // Same message either way avoids confirming/denying whether a
            // mobile number is registered (mirrors CustomerLoginService).
            return Result.Failure(Error.NotFound("PartnerLogin.NotFound", "No account found for this mobile number."));
        }

        return await _otpService.GenerateAsync(request.Mobile, OtpPurpose.Login);
    }

    public async Task<Result<PartnerLoginResponse>> LoginWithOtpAsync(LoginPartnerWithOtpRequest request)
    {
        var lockout = await CheckLockoutAsync(request.Mobile);
        if (lockout is not null)
        {
            return Result.Failure<PartnerLoginResponse>(lockout);
        }

        var otpResult = await _otpService.ValidateAsync(request.Mobile, request.OtpCode, OtpPurpose.Login);
        if (otpResult.IsFailure)
        {
            await RecordAttemptAsync(request.Mobile, succeeded: false);
            return Result.Failure<PartnerLoginResponse>(otpResult.Error);
        }

        var partner = await _partnerRepository.GetByPhoneAsync(request.Mobile);
        if (partner is null)
        {
            await RecordAttemptAsync(request.Mobile, succeeded: false);
            return Result.Failure<PartnerLoginResponse>(Error.NotFound("PartnerLogin.NotFound", "No account found for this mobile number."));
        }

        await RecordAttemptAsync(request.Mobile, succeeded: true);
        return await IssueSessionAsync(partner);
    }

    public async Task<Result<PartnerLoginResponse>> RefreshAsync(RefreshPartnerTokenRequest request)
    {
        var session = await _sessionRepository.GetByRefreshTokenHashAsync(Hash(request.RefreshToken));
        if (session is null || !session.IsActive(DateTime.UtcNow))
        {
            return Result.Failure<PartnerLoginResponse>(Error.Unauthorized("PartnerLogin.InvalidRefreshToken", "The refresh token is invalid or has expired."));
        }

        var partner = await _partnerRepository.GetByIdAsync(session.PartnerId);
        if (partner is null)
        {
            return Result.Failure<PartnerLoginResponse>(Error.Unauthorized("PartnerLogin.InvalidRefreshToken", "The refresh token is invalid or has expired."));
        }

        // Rotate on every use: the old refresh token is revoked immediately
        // so it cannot be replayed if intercepted (mirrors CustomerLoginService).
        session.Revoke();
        await _sessionRepository.UpdateAsync(session);

        return await IssueSessionAsync(partner, session.DeviceInfo, session.IpAddress);
    }

    public async Task<Result> LogoutAsync(LogoutPartnerRequest request)
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

    private async Task<Result<PartnerLoginResponse>> IssueSessionAsync(Partner partner, string? deviceInfo = null, string? ipAddress = null)
    {
        if (partner.Status is PartnerStatus.Suspended or PartnerStatus.Deactivated)
        {
            return Result.Failure<PartnerLoginResponse>(Error.Forbidden("PartnerLogin.AccountNotActive", "This account cannot log in."));
        }

        var accessToken = _tokenService.GenerateAccessToken(partner.Id, partner.Phone);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var now = DateTime.UtcNow;

        var session = new PartnerSession(
            Guid.NewGuid(), partner.Id, Hash(refreshToken), now, now.Add(_tokenService.RefreshTokenLifetime),
            deviceInfo, ipAddress);
        await _sessionRepository.AddAsync(session);

        return Result.Success(new PartnerLoginResponse(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken));
    }

    private async Task<Error?> CheckLockoutAsync(string identifier)
    {
        var since = DateTime.UtcNow.AddMinutes(-_accountOptions.LockoutWindowMinutes);
        int failures = await _loginAttemptRepository.CountFailuresSinceAsync(identifier, since);
        if (failures >= _accountOptions.MaxFailedLoginAttempts)
        {
            return Error.Forbidden("PartnerLogin.AccountLocked",
                $"Too many failed attempts. Try again in {_accountOptions.LockoutWindowMinutes} minutes.");
        }

        return null;
    }

    private Task RecordAttemptAsync(string identifier, bool succeeded) =>
        _loginAttemptRepository.AddAsync(new PartnerLoginAttempt(Guid.NewGuid(), identifier, succeeded, DateTime.UtcNow));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
