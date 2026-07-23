using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using backend.shared.Domain;

namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class SupportTicketCommentConfiguration : IEntityTypeConfiguration<SupportTicketComment>
    {
        public void Configure(EntityTypeBuilder<SupportTicketComment> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).UseIdentityColumn();

            builder.HasOne(t => t.SupportTicket)
                .WithMany()
                .HasForeignKey(t => t.SupportTicketId);

            builder.HasMany(t => t.Notifications)
                .WithOne(n => n.SupportTicketComment)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
