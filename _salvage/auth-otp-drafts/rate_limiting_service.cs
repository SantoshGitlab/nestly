namespace backend.shared.Application.Domain;

public class RateLimitingService : IRateLimitingService
{
    private readonly ILoginAttemptRepository _loginAttemptRepository;
    private const int MaxAttempts = 5;
    private const int LockoutDurationMinutes = 10;

    public RateLimitingService(ILoginAttemptRepository loginAttemptRepository)
    {
        _loginAttemptRepository = loginAttemptRepository;
    }

    public async Task<bool> IsAllowedAsync(string username)
    {
        if (await ExistsByUsernameAsync(username))
        {
            var attempts = await GetAttemptsByUsernameAsync(username);
            var recentAttempts = attempts.Where(a => a.AttemptTime >= DateTime.UtcNow.AddMinutes(-1)).ToList();

            if (recentAttempts.Count >= MaxAttempts)
            {
                var lastAttempt = recentAttempts.Last();
                if (lastAttempt.IsSuccessful == false && DateTime.UtcNow - lastAttempt.AttemptTime < TimeSpan.FromMinutes(LockoutDurationMinutes))
                {
                    return false;
                }
            }

            await _loginAttemptRepository.AddAsync(new LoginAttempt(username, true));
        }
        else
        {
            await _loginAttemptRepository.AddAsync(new LoginAttempt(username, true));
        }

        return true;
    }

    public async Task<bool> IsLockedOutAsync(string username)
    {
        var attempts = await GetAttemptsByUsernameAsync(username);
        var recentAttempts = attempts.Where(a => a.AttemptTime >= DateTime.UtcNow.AddMinutes(-1)).ToList();

        if (recentAttempts.Count >= MaxAttempts)
        {
            var lastAttempt = recentAttempts.Last();
            return !lastAttempt.IsSuccessful && DateTime.UtcNow - lastAttempt.AttemptTime < TimeSpan.FromMinutes(LockoutDurationMinutes);
        }

        return false;
    }

    private async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _loginAttemptRepository.ExistsByUsernameAsync(username);
    }

    private async Task<IEnumerable<LoginAttempt>> GetAttemptsByUsernameAsync(string username)
    {
        return await _loginAttemptRepository.GetAttemptsByUsernameAsync(username);
    }
}
