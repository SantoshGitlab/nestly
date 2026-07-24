using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.shared.Infrastructure.Persistence.Repositories
{
    public class ServiceMediaRepository : IRepository<ServiceMedia>
    {
        private readonly NestlyDbContext _context;

        public ServiceMediaRepository(NestlyDbContext context)
        {
            _context = context;
        }

        // ... rest of the code
    }
}
