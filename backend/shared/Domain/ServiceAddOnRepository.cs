using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain;
using backend.shared.Application.Domain.Entities;

namespace backend.shared.Application.Domain.Repositories
{
    public class ServiceAddOnRepository : IRepository<ServiceAddOn>
    {
        private readonly DbContext _context;

        public ServiceAddOnRepository(DbContext context) => _context = context;

        public async Task AddAsync(ServiceAddOn entity)
        {
            await _context.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServiceAddOn entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task<ServiceAddOn> GetByIdAsync(Guid id) => await _context.Set<ServiceAddOn>().FindAsync(id);

        public async Task<IEnumerable<ServiceAddOn>> GetAllAsync() => await _context.Set<ServiceAddOn>().ToListAsync();
    }
}
