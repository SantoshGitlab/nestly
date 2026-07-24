using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.shared.Infrastructure.Persistence.Repositories
{
    public class ServiceAddOnRepository : IRepository<ServiceAddOn>
    {
        private readonly NestlyDbContext _context;

        public ServiceAddOnRepository(NestlyDbContext context)
        {
            _context = context;
        }

        // ... rest of the code
    }
}
