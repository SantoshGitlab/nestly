using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using backend.shared.Application.Domain;

namespace backend.shared.Application.Domain.Repositories
{
    public class ServiceAddOnRepository : IRepository<ServiceAddOn>
    {
        private readonly DbContext _context;

        public ServiceAddOnRepository(DbContext context)
        {
            _context = context;
        }

        // ... rest of the code ...

        // Add a method to get all services for a given add-on
        public IEnumerable<Service> GetServicesByAddOnId(Guid addOnId)
        {
            return _context.Set<Service>().Where(s => s.AddOns.Any(a => a.Id == addOnId)).ToList();
        }
    }
}
