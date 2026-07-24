using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.shared.Infrastructure.Persistence.Services
{
    public class OTPService : IOtpService
    {
        private readonly NestlyDbContext _context;

        public OTPService(NestlyDbContext context)
        {
            _context = context;
        }

        // ... rest of the code
    }
}
