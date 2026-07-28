using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerAuthIdentityRepository : ICustomerAuthIdentityRepository
{
    private readonly NestlyDbContext _context;

    public CustomerAuthIdentityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerAuthIdentity entity)
    {
        await _context.Set<CustomerAuthIdentity>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerAuthIdentity entity)
    {
        _context.Set<CustomerAuthIdentity>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerAuthIdentity?> GetByProviderAsync(AuthProviderType provider, string identifier) =>
        _context.Set<CustomerAuthIdentity>()
            .FirstOrDefaultAsync(x => x.Provider == provider && x.Identifier == identifier);

    public async Task<IReadOnlyList<CustomerAuthIdentity>> GetByCustomerAsync(Guid customerId) =>
        await _context.Set<CustomerAuthIdentity>()
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

    public Task<bool> ExistsAsync(AuthProviderType provider, string identifier) =>
        _context.Set<CustomerAuthIdentity>().AnyAsync(x => x.Provider == provider && x.Identifier == identifier);
}
