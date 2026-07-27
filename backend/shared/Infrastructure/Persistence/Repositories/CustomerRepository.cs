using Microsoft.EntityFrameworkCore;
using Nestly.Application;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly NestlyDbContext _context;

    public CustomerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Customer entity)
    {
        await _context.Set<Customer>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Customer entity)
    {
        _context.Set<Customer>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Customer entity)
    {
        _context.Set<Customer>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Customer?> GetByIdAsync(Guid id) =>
        _context.Set<Customer>().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<Customer>> GetAllAsync() =>
        await _context.Set<Customer>().ToListAsync();

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Customer>().AnyAsync(c => c.Id == id);

    public Task<bool> ExistsByMobileAsync(string mobile) =>
        _context.Set<Customer>().AnyAsync(c => c.Mobile == mobile);

    public Task<bool> ExistsByEmailAsync(string email) =>
        _context.Set<Customer>().AnyAsync(c => c.Email == email);
}
