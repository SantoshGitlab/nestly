using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerCommunicationPreferenceRepository : ICustomerCommunicationPreferenceRepository
{
    private readonly NestlyDbContext _context;

    public CustomerCommunicationPreferenceRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerCommunicationPreference entity)
    {
        await _context.Set<CustomerCommunicationPreference>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerCommunicationPreference entity)
    {
        _context.Set<CustomerCommunicationPreference>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerCommunicationPreference?> GetByCustomerAsync(Guid customerId) =>
        _context.Set<CustomerCommunicationPreference>()
            .FirstOrDefaultAsync(p => p.CustomerId == customerId);
}
