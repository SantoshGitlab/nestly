using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class StateRepository : IStateRepository
{
    private readonly NestlyDbContext _context;

    public StateRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(State entity)
    {
        await _context.Set<State>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(State entity)
    {
        _context.Set<State>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<State?> GetByIdAsync(Guid id) =>
        _context.Set<State>().FirstOrDefaultAsync(s => s.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<State>().AnyAsync(s => s.Id == id);

    public async Task<IReadOnlyList<State>> ListAsync() =>
        await _context.Set<State>().OrderBy(s => s.Name).ToListAsync();

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null) =>
        _context.Set<State>().AnyAsync(s => s.Code == code && (excludeId == null || s.Id != excludeId));
}
