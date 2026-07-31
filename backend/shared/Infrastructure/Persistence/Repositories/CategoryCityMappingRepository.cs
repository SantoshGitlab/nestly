using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Serviceability;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CategoryCityMappingRepository : ICategoryCityMappingRepository
{
    private readonly NestlyDbContext _context;

    public CategoryCityMappingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CategoryCityMapping entity)
    {
        await _context.Set<CategoryCityMapping>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CategoryCityMapping entity)
    {
        _context.Set<CategoryCityMapping>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CategoryCityMapping?> GetByIdAsync(Guid id) =>
        _context.Set<CategoryCityMapping>().FirstOrDefaultAsync(m => m.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<CategoryCityMapping>().AnyAsync(m => m.Id == id);

    public Task<CategoryCityMapping?> FindAsync(Guid categoryId, Guid cityId) =>
        _context.Set<CategoryCityMapping>()
            .FirstOrDefaultAsync(m => m.CategoryId == categoryId && m.CityId == cityId);

    public async Task<IReadOnlyList<CategoryCityMappingResponse>> ListAsync(Guid? categoryId, Guid? cityId) =>
        await (
            from mapping in _context.Set<CategoryCityMapping>()
            join category in _context.Set<Category>() on mapping.CategoryId equals category.Id
            join city in _context.Set<City>() on mapping.CityId equals city.Id
            where (categoryId == null || mapping.CategoryId == categoryId) &&
                  (cityId == null || mapping.CityId == cityId)
            orderby city.Name, category.Name
            select new CategoryCityMappingResponse(
                mapping.Id, category.Id, category.Name, city.Id, city.Name, mapping.IsActive)
        ).ToListAsync();
}
