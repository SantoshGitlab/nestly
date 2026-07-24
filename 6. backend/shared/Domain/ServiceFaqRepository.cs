using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain.Repositories;

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
    }
}
