using Microsoft.EntityFrameworkCore;
using Nestly.Application.Customers;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerNoteRepository : ICustomerNoteRepository
{
    private readonly NestlyDbContext _context;

    public CustomerNoteRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerNote note)
    {
        await _context.Set<CustomerNote>().AddAsync(note);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CustomerNote>> ListByCustomerAsync(Guid customerId) =>
        await _context.Set<CustomerNote>()
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();
}
