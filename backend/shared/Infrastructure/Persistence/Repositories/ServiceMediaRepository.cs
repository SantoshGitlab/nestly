using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceMediaRepository : IServiceMediaRepository
{
    private readonly NestlyDbContext _context;

    public ServiceMediaRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceMedia entity)
    {
        await _context.Set<ServiceMedia>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ServiceMedia entity)
    {
        _context.Set<ServiceMedia>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceMedia?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceMedia>().FirstOrDefaultAsync(m => m.Id == id);

    public async Task<IReadOnlyList<ServiceMedia>> ListByServiceAsync(Guid serviceId) =>
        await _context.Set<ServiceMedia>().Where(m => m.ServiceId == serviceId).ToListAsync();
}
