using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerSessionRepository : ICustomerSessionRepository
{
    private readonly NestlyDbContext _context;

    public CustomerSessionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerSession entity)
    {
        await _context.Set<CustomerSession>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerSession entity)
    {
        _context.Set<CustomerSession>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash) =>
        _context.Set<CustomerSession>().FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);
}
