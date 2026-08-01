using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerLoginAttemptRepository
{
    Task AddAsync(PartnerLoginAttempt entity);
    Task<int> CountFailuresSinceAsync(string identifier, DateTime sinceUtc);
}
