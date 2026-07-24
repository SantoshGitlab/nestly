using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using backend.shared.Application.Domain;

namespace backend.shared.Application.Domain.Repositories
{
    public class ServiceMediaRepository : IRepository<ServiceMedia>
    {
        private readonly DbContext _context;

        public ServiceMediaRepository(DbContext context)
        {
            _context = context;
        }

        // ... rest of the code ...

        // Add a method to get all services for a given media
        public IEnumerable<Service> GetServicesByMediaId(Guid mediaId)
        {
            return _context.Set<Service>().Where(s => s.Medias.Any(m => m.Id == mediaId)).ToList();
        }
    }
}
