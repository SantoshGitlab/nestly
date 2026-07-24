using Microsoft.EntityFrameworkCore;
using backend.shared.Infrastructure.Persistence.Configurations;
using backend.shared.Domain;

namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class SupportTicketCommentConfiguration : IEntityTypeConfiguration<SupportTicketComment>
    {
        public void Configure(EntityTypeBuilder<SupportTicketComment> builder)
        {
            // ... rest of the code ...

            // Add a navigation property for the support ticket
            builder.HasOne(c => c.SupportTicket).WithMany(t => t.Comments).HasForeignKey(c => c.SupportTicketId);
        }
    }
}
