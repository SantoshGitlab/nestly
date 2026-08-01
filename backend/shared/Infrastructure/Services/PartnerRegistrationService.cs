using Nestly.Application;
using Nestly.Application.PartnerIdentity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Partner registration orchestration (task 146a), structurally mirroring
/// <see cref="CustomerRegistrationService"/>. No welcome-notification trigger
/// here (unlike the customer flow): <c>NotificationEvent.CustomerId</c> has a
/// real foreign key to the customer table, so it cannot record a partner
/// actor without a schema change - out of scope for this pass.
/// </summary>
public class PartnerRegistrationService : IPartnerRegistrationService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerAuthIdentityRepository _authIdentityRepository;
    private readonly IPartnerOtpService _otpService;

    public PartnerRegistrationService(
        IPartnerRepository partnerRepository,
        IPartnerAuthIdentityRepository authIdentityRepository,
        IPartnerOtpService otpService)
    {
        _partnerRepository = partnerRepository;
        _authIdentityRepository = authIdentityRepository;
        _otpService = otpService;
    }

    public async Task<Result> RequestOtpAsync(RequestPartnerRegistrationOtpRequest request)
    {
        if (await _partnerRepository.ExistsByPhoneAsync(request.Mobile))
        {
            return Result.Failure(Error.Conflict("PartnerRegistration.MobileAlreadyRegistered",
                "A partner with this mobile number already exists."));
        }

        return await _otpService.GenerateAsync(request.Mobile, OtpPurpose.Registration);
    }

    public async Task<Result<PartnerSummaryResponse>> RegisterAsync(RegisterPartnerRequest request)
    {
        if (!request.ConsentAccepted)
        {
            return Result.Failure<PartnerSummaryResponse>(Error.Validation(
                "PartnerRegistration.ConsentRequired", "Consent to Terms & Privacy is required."));
        }

        var otpResult = await _otpService.ValidateAsync(request.Mobile, request.OtpCode, OtpPurpose.Registration);
        if (otpResult.IsFailure)
        {
            return Result.Failure<PartnerSummaryResponse>(otpResult.Error);
        }

        if (await _partnerRepository.ExistsByPhoneAsync(request.Mobile))
        {
            return Result.Failure<PartnerSummaryResponse>(Error.Conflict(
                "PartnerRegistration.MobileAlreadyRegistered", "A partner with this mobile number already exists."));
        }

        // OTP proved mobile ownership only, not KYC - Partner's constructor
        // starts the account PendingVerification, not Active (OPEN DECISIONS
        // #2 in PARTNER.md constrains this to Individual for v1).
        var partner = new Partner(
            Guid.NewGuid(), request.LegalName, request.DisplayName, PartnerType.Individual, request.Mobile, request.Email);
        await _partnerRepository.AddAsync(partner);

        var mobileIdentity = new PartnerAuthIdentity(
            Guid.NewGuid(), partner.Id, AuthProviderType.MobileOtp, request.Mobile, isPrimary: true);
        await _authIdentityRepository.AddAsync(mobileIdentity);

        return Result.Success(new PartnerSummaryResponse(
            partner.Id, partner.LegalName, partner.DisplayName, partner.Phone, partner.Email,
            partner.Status.ToString(), partner.OnboardingStatus.ToString()));
    }
}
