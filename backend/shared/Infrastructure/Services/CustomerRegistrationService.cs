using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.Application.Notifications;
using Nestly.Application.Referral;
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
///
/// Also the welcome-notification trigger (SRS 19.1, task 88a): dispatched
/// directly here rather than through the domain-event/MediatR pattern the
/// other triggers use, because <see cref="Customer"/> is a plain
/// <c>Entity&lt;Guid&gt;</c>, not an <c>AggregateRoot</c> - it has no
/// domain-event mechanism to hook into.
/// </summary>
public class CustomerRegistrationService : ICustomerRegistrationService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAuthIdentityRepository _authIdentityRepository;
    private readonly IOTPService _otpService;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly IReferralRepository _referralRepository;
    private readonly IReferralProgramConfigRepository _referralProgramConfigRepository;
    private readonly ILogger<CustomerRegistrationService> _logger;
    private readonly AccountOptions _options;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public CustomerRegistrationService(
        ICustomerRepository customerRepository,
        ICustomerAuthIdentityRepository authIdentityRepository,
        IOTPService otpService,
        INotificationDispatchService notificationDispatchService,
        IReferralRepository referralRepository,
        IReferralProgramConfigRepository referralProgramConfigRepository,
        ILogger<CustomerRegistrationService> logger,
        IOptions<AccountOptions> options)
    {
        _customerRepository = customerRepository;
        _authIdentityRepository = authIdentityRepository;
        _otpService = otpService;
        _notificationDispatchService = notificationDispatchService;
        _referralRepository = referralRepository;
        _referralProgramConfigRepository = referralProgramConfigRepository;
        _logger = logger;
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

        await _notificationDispatchService.DispatchAsync(
            customer.Id,
            NotificationEventType.Welcome,
            new NotificationRecipient(customer.Mobile, customer.Email),
            new Dictionary<string, string> { ["CustomerName"] = customer.Name });

        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            await TryCreateReferralAsync(customer, request.ReferralCode);
        }

        return Result.Success(new CustomerSummaryResponse(
            customer.Id, customer.Mobile, customer.Email, customer.Name, customer.Status.ToString()));
    }

    /// <summary>
    /// Task 163 (REFERRAL.md): best-effort - an invalid or self-referential
    /// code never fails registration itself, it just means no Referral row
    /// gets created. The customer the code belongs to already exists (this
    /// is the referee registering after the referrer shared their code), so
    /// every failure mode here is either a bad/stale code or abuse, neither
    /// of which should block a legitimate signup.
    /// </summary>
    private async Task TryCreateReferralAsync(Customer referee, string referralCode)
    {
        Customer? referrer = await _customerRepository.GetByReferralCodeAsync(referralCode);
        if (referrer is null)
        {
            _logger.LogInformation("Registration referral code {ReferralCode} did not match any customer.", referralCode);
            return;
        }

        // Self-referral block (REFERRAL.md "FRAUD / ABUSE PREVENTION" /
        // OPEN DECISIONS #2): by mobile/email match, not customer id - the
        // referee is a brand-new account, so id equality can never trigger;
        // the real risk is the same person's second account using their own
        // code. In practice this never fires today: Customer.Mobile/Email
        // are both unconditionally DB-unique (CustomerConfiguration), so a
        // genuine same-mobile-or-email attempt already fails earlier on
        // Registration.MobileAlreadyRegistered/EmailAlreadyRegistered (see
        // ReferralRegistrationTests for the confirming test) - kept anyway
        // as defense in depth against either uniqueness constraint being
        // relaxed later, per REFERRAL.md explicitly asking for this check
        // "at registration, not just at reward time".
        bool sameMobile = referrer.Mobile == referee.Mobile;
        bool sameEmail = referrer.Email is not null && referee.Email is not null && referrer.Email == referee.Email;
        if (sameMobile || sameEmail)
        {
            _logger.LogWarning(
                "Registration blocked a self-referral attempt: customer {CustomerId} tried to use their own referral code.",
                referrer.Id);
            return;
        }

        // One referral per referee, ever - the unique index on
        // referee_customer_id (task 161) is the real backstop; this check
        // exists so a stale/duplicate submission gets a clean no-op instead
        // of an unhandled constraint-violation exception, mirroring how
        // ReviewService/CouponRedemption treat their own unique indexes as
        // the arbiter of a race, not the sole line of defense.
        if (await _referralRepository.GetByRefereeCustomerIdAsync(referee.Id) is not null)
        {
            return;
        }

        ReferralProgramConfig? config = await _referralProgramConfigRepository.GetAsync();
        if (config is null || !config.IsActive)
        {
            _logger.LogInformation("Registration referral code {ReferralCode} ignored: referral program is not active.", referralCode);
            return;
        }

        var referral = new Domain.Referral(Guid.NewGuid(), referrer.Id, referee.Id, referralCode, config);
        await _referralRepository.AddAsync(referral);

        await _notificationDispatchService.DispatchAsync(
            referrer.Id,
            NotificationEventType.ReferralRegistered,
            new NotificationRecipient(referrer.Mobile, referrer.Email),
            new Dictionary<string, string> { ["RefereeName"] = referee.Name });
    }
}
