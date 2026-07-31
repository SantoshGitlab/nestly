using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ExportJobRepository : IExportJobRepository
{
    private readonly NestlyDbContext _context;

    public ExportJobRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ExportJob entity)
    {
        await _context.Set<ExportJob>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ExportJob entity)
    {
        _context.Set<ExportJob>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ExportJob?> GetByIdAsync(Guid id) =>
        _context.Set<ExportJob>().FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ExportJob>().AnyAsync(x => x.Id == id);

    public async Task<IReadOnlyList<ExportJob>> ListByRequesterAsync(Guid requestedByAdminUserId) =>
        await _context.Set<ExportJob>()
            .Where(x => x.RequestedByAdminUserId == requestedByAdminUserId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToListAsync();
}
