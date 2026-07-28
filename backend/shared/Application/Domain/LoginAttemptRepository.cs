using Nestly.Domain;

namespace Nestly.Application;

public interface ILoginAttemptRepository
{
    Task AddAsync(LoginAttempt entity);
    Task<int> CountFailuresSinceAsync(string identifier, DateTime sinceUtc);
}
