using Microsoft.EntityFrameworkCore;
using backend.shared.Infrastructure.Persistence.Configurations;

namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class SupportTicketCommentConfiguration : IEntityTypeConfiguration<SupportTicketComment>
    {
        public void Configure(EntityTypeBuilder<SupportTicketComment> builder)
        {
            builder.HasOne(c => c.SupportTicket).WithMany(t => t.Comments).HasForeignKey(c => c.SupportTicketId);
        }
    }
}
