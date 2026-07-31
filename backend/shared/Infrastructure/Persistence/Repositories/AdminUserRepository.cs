using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly NestlyDbContext _context;

    public AdminUserRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AdminUser entity)
    {
        await _context.Set<AdminUser>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AdminUser entity)
    {
        _context.Set<AdminUser>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<AdminUser?> GetByIdAsync(Guid id) =>
        _context.Set<AdminUser>().FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<AdminUser>().AnyAsync(x => x.Id == id);

    public Task<AdminUser?> GetByEmailAsync(string email) =>
        _context.Set<AdminUser>().FirstOrDefaultAsync(x => x.Email == email);
}
