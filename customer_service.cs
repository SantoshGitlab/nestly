namespace backend.shared.Application.Domain;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOTPService _otpService;
    private readonly IRateLimitingService _rateLimitingService;

    public CustomerService(ICustomerRepository customerRepository, IOTPService otpService, IRateLimitingService rateLimitingService)
    {
        _customerRepository = customerRepository;
        _otpService = otpService;
        _rateLimitingService = rateLimitingService;
    }

    public async Task CreateAsync(Customer customer)
    {
        // Implementation remains the same
    }

    public async Task UpdateAsync(Customer customer)
    {
        // Implementation remains the same
    }

    public async Task DeleteAsync(Guid id)
    {
        // Implementation remains the same
    }

    public async Task<Customer> GetByIdAsync(Guid id)
    {
        // Implementation remains the same
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        // Implementation remains the same
    }

    public async Task<ValidationResult> RegisterAsync(string username, string password, string mobile, string email)
    {
        // Implementation remains the same
    }

    public async Task<ValidationResult> LoginAsync(string username, string password)
    {
        if (await _rateLimitingService.IsLockedOutAsync(username))
        {
            return ValidationResult.Failed(new Error("ACCOUNT_LOCKED", "Your account is locked due to too many failed login attempts. Please try again later.", ErrorType.Business));
        }

        var customer = await _customerRepository.GetByIdAsync(username);
        if (customer == null)
        {
            await _rateLimitingService.AddAttemptAsync(username, false);
            return ValidationResult.Failed(new Error("INVALID_CREDENTIALS", "Invalid username or password.", ErrorType.Business));
        }

        // Password validation logic remains the same

        await _rateLimitingService.AddAttemptAsync(username, true);
        return ValidationResult.Success();
    }

    public async Task<ValidationResult> ValidateOTPAsync(string phoneNumber, string providedOTP)
    {
        if (await _rateLimitingService.IsLockedOutAsync(phoneNumber))
        {
            return ValidationResult.Failed(new Error("ACCOUNT_LOCKED", "Your account is locked due to too many failed OTP attempts. Please try again later.", ErrorType.Business));
        }

        var result = await _otpService.ValidateAsync(phoneNumber, providedOTP);
        if (result.IsFailure)
        {
            await _rateLimitingService.AddAttemptAsync(phoneNumber, false);
            return ValidationResult.Failed(result.Error);
        }

        await _rateLimitingService.AddAttemptAsync(phoneNumber, true);
        return ValidationResult.Success();
    }
}
