using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Registration orchestration (SRS 11.2.1). Lives in Infrastructure rather
/// than Application, matching <see cref="OtpService"/>: it needs
/// <see cref="AccountOptions"/> (an Infrastructure-bound config type), and
/// Application cannot depend on Infrastructure without inverting the
/// project's dependency direction.
/// </summary>
public class CustomerRegistrationService : ICustomerRegistrationService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAuthIdentityRepository _authIdentityRepository;
    private readonly IOTPService _otpService;
    private readonly AccountOptions _options;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public CustomerRegistrationService(
        ICustomerRepository customerRepository,
        ICustomerAuthIdentityRepository authIdentityRepository,
        IOTPService otpService,
        IOptions<AccountOptions> options)
    {
        _customerRepository = customerRepository;
        _authIdentityRepository = authIdentityRepository;
        _otpService = otpService;
        _options = options.Value;
    }

    public async Task<Result> RequestOtpAsync(RequestRegistrationOtpRequest request)
    {
        if (await _customerRepository.ExistsByMobileAsync(request.Mobile))
        {
            return Result.Failure(Error.Conflict("Registration.MobileAlreadyRegistered",
                "A customer with this mobile number already exists."));
        }

        return await _otpService.GenerateAsync(request.Mobile, OtpPurpose.Registration);
    }

    public async Task<Result<CustomerSummaryResponse>> RegisterAsync(RegisterCustomerRequest request)
    {
        if (!request.ConsentAccepted)
        {
            return Result.Failure<CustomerSummaryResponse>(Error.Validation(
                "Registration.ConsentRequired", "Consent to Terms & Privacy is required."));
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            if (!_options.PasswordAuthEnabled)
            {
                return Result.Failure<CustomerSummaryResponse>(Error.Validation(
                    "Registration.PasswordAuthDisabled", "Password-based authentication is not enabled."));
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure<CustomerSummaryResponse>(Error.Validation(
                    "Registration.EmailRequiredForPassword", "Email is required when setting a password."));
            }
        }

        var otpResult = await _otpService.ValidateAsync(request.Mobile, request.OtpCode, OtpPurpose.Registration);
        if (otpResult.IsFailure)
        {
            return Result.Failure<CustomerSummaryResponse>(otpResult.Error);
        }

        if (await _customerRepository.ExistsByMobileAsync(request.Mobile))
        {
            return Result.Failure<CustomerSummaryResponse>(Error.Conflict(
                "Registration.MobileAlreadyRegistered", "A customer with this mobile number already exists."));
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && _options.RequireUniqueEmail &&
            await _customerRepository.ExistsByEmailAsync(request.Email))
        {
            return Result.Failure<CustomerSummaryResponse>(Error.Conflict(
                "Registration.EmailAlreadyRegistered", "A customer with this email already exists."));
        }

        // OTP already proved mobile ownership, so the account starts Active
        // rather than Unverified — there is nothing left to verify.
        var customer = new Customer(Guid.NewGuid(), request.Mobile, request.Name, CustomerStatus.Active, request.Email);
        await _customerRepository.AddAsync(customer);

        var mobileIdentity = new CustomerAuthIdentity(
            Guid.NewGuid(), customer.Id, AuthProviderType.MobileOtp, request.Mobile, isPrimary: true);
        await _authIdentityRepository.AddAsync(mobileIdentity);

        if (!string.IsNullOrEmpty(request.Password) && !string.IsNullOrWhiteSpace(request.Email))
        {
            var emailIdentity = new CustomerAuthIdentity(
                Guid.NewGuid(), customer.Id, AuthProviderType.EmailPassword, request.Email, isPrimary: false);
            emailIdentity.SetPasswordHash(_passwordHasher.HashPassword(customer, request.Password));
            await _authIdentityRepository.AddAsync(emailIdentity);
        }

        return Result.Success(new CustomerSummaryResponse(
            customer.Id, customer.Mobile, customer.Email, customer.Name, customer.Status.ToString()));
    }
}
