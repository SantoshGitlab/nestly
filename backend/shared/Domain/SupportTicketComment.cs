using Microsoft.EntityFrameworkCore;
using backend.shared.Infrastructure.Persistence.Configurations;

namespace backend.shared.Domain
{
    public class SupportTicketComment : Entity<Guid>
    {
        private Guid _supportTicketId;
        // ... other properties and methods

        public void SetSupportTicketId(Guid supportTicketId) => this._supportTicketId = supportTicketId;

        public override async Task OnModelCreatingAsync(ModelBuilder modelBuilder)
        {
            base.OnModelCreatingAsync(modelBuilder);
            modelBuilder.ApplyConfiguration<SupportTicketComment>(new SupportTicketCommentConfiguration());
        }
    }
}
