using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerAddressRepository : ICustomerAddressRepository
{
    private readonly NestlyDbContext _context;

    public CustomerAddressRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerAddress entity)
    {
        await _context.Set<CustomerAddress>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerAddress entity)
    {
        _context.Set<CustomerAddress>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(CustomerAddress entity)
    {
        _context.Set<CustomerAddress>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerAddress?> GetByIdAsync(Guid id) =>
        _context.Set<CustomerAddress>().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId) =>
        await _context.Set<CustomerAddress>()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync();

    public Task<CustomerAddress?> GetDefaultAsync(Guid customerId) =>
        _context.Set<CustomerAddress>().FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsDefault);
}
