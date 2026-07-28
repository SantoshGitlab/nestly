using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly NestlyDbContext _context;

    public LoginAttemptRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LoginAttempt entity)
    {
        await _context.Set<LoginAttempt>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountFailuresSinceAsync(string identifier, DateTime sinceUtc) =>
        _context.Set<LoginAttempt>()
            .CountAsync(a => a.Identifier == identifier && !a.Succeeded && a.OccurredAtUtc >= sinceUtc);
}
