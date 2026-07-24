using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.shared.Infrastructure.Persistence.Repositories
{
    public class ServiceFaqRepository : IRepository<ServiceFaq>
    {
        private readonly NestlyDbContext _context;

        public ServiceFaqRepository(NestlyDbContext context)
        {
            _context = context;
        }

        // ... rest of the code
    }
}
