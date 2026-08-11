using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class AdminRoleRepository : IAdminRoleRepository
{
    private readonly NestlyDbContext _context;

    public AdminRoleRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AdminRole entity)
    {
        await _context.Set<AdminRole>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AdminRole entity)
    {
        _context.Set<AdminRole>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<AdminRole?> GetByIdAsync(Guid id) =>
        _context.Set<AdminRole>().FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<AdminRole>().AnyAsync(x => x.Id == id);

    public async Task<IReadOnlyList<AdminRole>> ListAllAsync() =>
        await _context.Set<AdminRole>().OrderBy(x => x.Name).ToListAsync();

    public Task<AdminRole?> GetByNameAsync(string name) =>
        _context.Set<AdminRole>().FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());
}
