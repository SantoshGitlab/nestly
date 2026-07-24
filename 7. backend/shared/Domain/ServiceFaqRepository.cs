using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using backend.shared.Application.Domain;

namespace backend.shared.Application.Domain.Repositories
{
    public class ServiceFaqRepository : IRepository<ServiceFaq>
    {
        private readonly DbContext _context;

        public ServiceFaqRepository(DbContext context)
        {
            _context = context;
        }

        // ... rest of the code ...

        // Add a method to get all services for a given FAQ
        public IEnumerable<Service> GetServicesByFaqId(Guid faqId)
        {
            return _context.Set<Service>().Where(s => s.Faqs.Any(f => f.Id == faqId)).ToList();
        }
    }
}
