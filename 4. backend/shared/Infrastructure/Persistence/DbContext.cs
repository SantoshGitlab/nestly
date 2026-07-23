using Microsoft.EntityFrameworkCore;
using backend.shared.Domain;
using backend.shared.Application.Domain;

namespace backend.shared.Infrastructure.Persistence
{
    public class NestlyDbContext : DbContext
    {
        // ... other code ...

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ... other configuration ...

            modelBuilder.ApplyConfiguration(new ReviewConfiguration());
            modelBuilder.ApplyConfiguration(new SupportTicketConfiguration());
            modelBuilder.ApplyConfiguration(new SupportTicketCommentConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationEventConfiguration());
        }
    }
}
