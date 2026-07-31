using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.AdminUserManagement;
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

    public async Task<IReadOnlyList<AdminUser>> ListActiveAsync() =>
        await _context.Set<AdminUser>()
            .Where(x => x.Status == AdminUserStatus.Active)
            .OrderBy(x => x.FullName)
            .ToListAsync();

    public async Task<AdminUserSearchResult> SearchAsync(AdminUserSearchFilter filter)
    {
        var query = _context.Set<AdminUser>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string term = filter.Email.ToLower();
            query = query.Where(x => x.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            string term = filter.Name.ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.RoleId.HasValue)
        {
            query = query.Where(x => x.RoleId == filter.RoleId.Value);
        }

        int totalCount = await query.CountAsync();

        var adminUsers = await query
            .OrderBy(x => x.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var roleIds = adminUsers.Where(x => x.RoleId.HasValue).Select(x => x.RoleId!.Value).Distinct().ToList();
        var roleNamesById = roleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<AdminRole>()
                .Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name);

        var rows = adminUsers
            .Select(x => new AdminUserSearchRow(
                x, x.RoleId.HasValue && roleNamesById.TryGetValue(x.RoleId.Value, out var name) ? name : null))
            .ToList();

        return new AdminUserSearchResult(rows, totalCount);
    }
}
