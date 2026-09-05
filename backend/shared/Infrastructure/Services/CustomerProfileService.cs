using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Profile;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Profile management (SRS 11.2.3). Lives in Infrastructure for the same
/// reason as the registration/login services: it needs
/// <see cref="AccountOptions"/>, an Infrastructure-bound config type.
///
/// Mobile and email are identity-bearing — the mobile is the login identifier
/// for OTP auth and the email for password auth — so neither can be edited by
/// the plain profile PUT. Each has its own request/confirm pair where an OTP
/// sent to the *new* contact detail proves the customer actually controls it
/// before the account starts trusting it.
/// </summary>
public class CustomerProfileService : ICustomerProfileService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAuthIdentityRepository _authIdentityRepository;
    private readonly ICustomerCommunicationPreferenceRepository _preferenceRepository;
    private readonly ICustomerSessionRepository _sessionRepository;
    private readonly IOTPService _otpService;
    private readonly AccountOptions _options;
    private readonly ILogger<CustomerProfileService> _logger;

    public CustomerProfileService(
        ICustomerRepository customerRepository,
        ICustomerAuthIdentityRepository authIdentityRepository,
        ICustomerCommunicationPreferenceRepository preferenceRepository,
        ICustomerSessionRepository sessionRepository,
        IOTPService otpService,
        IOptions<AccountOptions> options,
        ILogger<CustomerProfileService> logger)
    {
        _customerRepository = customerRepository;
        _authIdentityRepository = authIdentityRepository;
        _preferenceRepository = preferenceRepository;
        _sessionRepository = sessionRepository;
        _otpService = otpService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<CustomerProfileResponse>> GetAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        return customer is null
            ? Result.Failure<CustomerProfileResponse>(NotFound())
            : Result.Success(ToResponse(customer));
    }

    public async Task<Result<CustomerProfileResponse>> UpdateAsync(Guid customerId, UpdateProfileRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure<CustomerProfileResponse>(NotFound());
        }

        customer.UpdateProfileDetails(
            request.Name, request.DateOfBirth, request.City, request.State, request.Pincode, request.Country);
        await _customerRepository.UpdateAsync(customer);

        return Result.Success(ToResponse(customer));
    }

    public async Task<Result> RequestMobileChangeOtpAsync(Guid customerId, RequestMobileChangeOtpRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure(NotFound());
        }

        if (string.Equals(customer.Mobile, request.NewMobile, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation("Profile.MobileUnchanged",
                "The new mobile number is the same as the current one."));
        }

        // Checked before sending: mobile is unique on the customer table, so
        // letting the OTP go out for a taken number would only end in a
        // confusing failure at confirm time.
        if (await _customerRepository.ExistsByMobileAsync(request.NewMobile))
        {
            return Result.Failure(Error.Conflict("Profile.MobileAlreadyRegistered",
                "This mobile number is already registered to another account."));
        }

        return await _otpService.GenerateAsync(request.NewMobile, OtpPurpose.MobileChange);
    }

    public async Task<Result<CustomerProfileResponse>> ConfirmMobileChangeAsync(Guid customerId, ConfirmMobileChangeRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure<CustomerProfileResponse>(NotFound());
        }

        // Re-checked after the OTP was issued: another account could have
        // claimed the number in between.
        if (await _customerRepository.ExistsByMobileAsync(request.NewMobile))
        {
            return Result.Failure<CustomerProfileResponse>(Error.Conflict("Profile.MobileAlreadyRegistered",
                "This mobile number is already registered to another account."));
        }

        var otpResult = await _otpService.ValidateAsync(request.NewMobile, request.OtpCode, OtpPurpose.MobileChange);
        if (otpResult.IsFailure)
        {
            return Result.Failure<CustomerProfileResponse>(otpResult.Error);
        }

        string previousMobile = customer.Mobile;
        customer.ChangeMobile(request.NewMobile);
        await _customerRepository.UpdateAsync(customer);

        // The MobileOtp auth identity keys the OTP login path off the mobile
        // number. Leaving it on the old value would let the previous number
        // keep logging in and lock the new one out.
        var mobileIdentity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.MobileOtp, previousMobile);
        if (mobileIdentity is not null)
        {
            mobileIdentity.ChangeIdentifier(request.NewMobile);
            await _authIdentityRepository.UpdateAsync(mobileIdentity);
        }
        else
        {
            _logger.LogWarning("Customer {CustomerId} changed mobile but had no MobileOtp auth identity to update.", customerId);
        }

        return Result.Success(ToResponse(customer));
    }

    public async Task<Result> RequestEmailChangeOtpAsync(Guid customerId, RequestEmailChangeOtpRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure(NotFound());
        }

        if (string.Equals(customer.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation("Profile.EmailUnchanged",
                "The new email address is the same as the current one."));
        }

        if (_options.RequireUniqueEmail && await _customerRepository.ExistsByEmailAsync(request.NewEmail))
        {
            return Result.Failure(Error.Conflict("Profile.EmailAlreadyRegistered",
                "This email address is already registered to another account."));
        }

        // Delivered to the address being claimed, not to the one on file —
        // sending to the current address would prove nothing about the new one.
        return await _otpService.GenerateAsync(request.NewEmail, OtpPurpose.EmailChange, NotificationChannel.Email);
    }

    public async Task<Result<CustomerProfileResponse>> ConfirmEmailChangeAsync(Guid customerId, ConfirmEmailChangeRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure<CustomerProfileResponse>(NotFound());
        }

        if (_options.RequireUniqueEmail && await _customerRepository.ExistsByEmailAsync(request.NewEmail))
        {
            return Result.Failure<CustomerProfileResponse>(Error.Conflict("Profile.EmailAlreadyRegistered",
                "This email address is already registered to another account."));
        }

        var otpResult = await _otpService.ValidateAsync(request.NewEmail, request.OtpCode, OtpPurpose.EmailChange);
        if (otpResult.IsFailure)
        {
            return Result.Failure<CustomerProfileResponse>(otpResult.Error);
        }

        string? previousEmail = customer.Email;
        customer.ChangeEmail(request.NewEmail);
        await _customerRepository.UpdateAsync(customer);

        // If password auth was set up, its identity is keyed on the email —
        // move it too, or the customer's password login stops resolving.
        if (!string.IsNullOrWhiteSpace(previousEmail))
        {
            var emailIdentity = await _authIdentityRepository.GetByProviderAsync(AuthProviderType.EmailPassword, previousEmail);
            if (emailIdentity is not null)
            {
                emailIdentity.ChangeIdentifier(request.NewEmail);
                await _authIdentityRepository.UpdateAsync(emailIdentity);
            }
        }

        return Result.Success(ToResponse(customer));
    }

    public async Task<Result<CommunicationPreferencesResponse>> GetPreferencesAsync(Guid customerId)
    {
        var preference = await _preferenceRepository.GetByCustomerAsync(customerId);
        if (preference is not null)
        {
            return Result.Success(ToResponse(preference));
        }

        if (!await _customerRepository.ExistsAsync(customerId))
        {
            return Result.Failure<CommunicationPreferencesResponse>(NotFound());
        }

        // Reading preferences must not create a row — a GET stays safe and
        // idempotent. Customers registered before this table existed simply
        // see the defaults until they save a change.
        return Result.Success(ToResponse(CustomerCommunicationPreference.CreateDefault(Guid.Empty, customerId)));
    }

    public async Task<Result<CommunicationPreferencesResponse>> UpdatePreferencesAsync(
        Guid customerId, CommunicationPreferencesRequest request)
    {
        if (!await _customerRepository.ExistsAsync(customerId))
        {
            return Result.Failure<CommunicationPreferencesResponse>(NotFound());
        }

        var preference = await _preferenceRepository.GetByCustomerAsync(customerId);
        bool isNew = preference is null;
        preference ??= CustomerCommunicationPreference.CreateDefault(Guid.NewGuid(), customerId);

        preference.Update(
            request.TransactionalSms,
            request.TransactionalEmail,
            request.TransactionalWhatsApp,
            request.PromotionalSms,
            request.PromotionalEmail,
            request.PromotionalWhatsApp,
            request.Push);

        if (isNew)
        {
            await _preferenceRepository.AddAsync(preference);
        }
        else
        {
            await _preferenceRepository.UpdateAsync(preference);
        }

        return Result.Success(ToResponse(preference));
    }

    public async Task<Result> DeleteAccountAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure(NotFound());
        }

        if (customer.Status == CustomerStatus.SoftDeleted)
        {
            // Already deleted - idempotent from the caller's perspective
            // rather than an error, since the end state they wanted holds.
            return Result.Success();
        }

        customer.SoftDelete();
        await _customerRepository.UpdateAsync(customer);
        await _sessionRepository.RevokeAllForCustomerAsync(customerId);

        _logger.LogInformation("Customer {CustomerId} deleted their own account.", customerId);
        return Result.Success();
    }

    private static Error NotFound() => Error.NotFound("Profile.NotFound", "Customer profile not found.");

    private static CustomerProfileResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Mobile,
        customer.Email,
        customer.Name,
        customer.DateOfBirth,
        customer.City,
        customer.State,
        customer.Pincode,
        customer.Country,
        customer.Status.ToString(),
        customer.CreatedAt,
        customer.UpdatedAt);

    private static CommunicationPreferencesResponse ToResponse(CustomerCommunicationPreference preference) => new(
        preference.TransactionalSmsEnabled,
        preference.TransactionalEmailEnabled,
        preference.TransactionalWhatsAppEnabled,
        preference.PromotionalSmsEnabled,
        preference.PromotionalEmailEnabled,
        preference.PromotionalWhatsAppEnabled,
        preference.PushEnabled,
        preference.UpdatedAt);
}
