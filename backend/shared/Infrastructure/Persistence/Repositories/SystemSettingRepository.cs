using Microsoft.EntityFrameworkCore;
using Nestly.Application.Settings;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly NestlyDbContext _context;

    public SystemSettingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<SystemSetting?> GetByGroupKeyAsync(string groupKey, CancellationToken cancellationToken = default) =>
        _context.Set<SystemSetting>().FirstOrDefaultAsync(s => s.GroupKey == groupKey, cancellationToken);

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<SystemSetting>().ToListAsync(cancellationToken);

    public async Task UpdateAsync(SystemSetting setting, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(setting).State == EntityState.Detached)
        {
            _context.Set<SystemSetting>().Update(setting);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
