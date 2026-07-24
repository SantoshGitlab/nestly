using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain.Repositories;

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
    }
}
