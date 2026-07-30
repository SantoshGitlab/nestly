using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly NestlyDbContext _context;

    public CategoryRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Category entity)
    {
        await _context.Set<Category>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Category entity)
    {
        _context.Set<Category>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Category?> GetByIdAsync(Guid id) =>
        _context.Set<Category>().FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Category>().AnyAsync(c => c.Id == id);

    public Task<Category?> GetBySlugAsync(string slug) =>
        _context.Set<Category>().FirstOrDefaultAsync(c => c.Slug == slug);

    public Task<bool> ExistsBySlugAsync(string slug) =>
        _context.Set<Category>().AnyAsync(c => c.Slug == slug);
}
