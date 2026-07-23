using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using backend.shared.Application.Domain;

namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class SupportTicketCommentConfiguration : IEntityTypeConfiguration<SupportTicketComment>
    {
        public void Configure(EntityTypeBuilder<SupportTicketComment> builder)
        {
            builder.ToTable("support_ticket_comments");

            builder.HasKey(stc => stc.Id);

            builder.Property(stc => stc.Id).UseIdentityColumn();

            builder.Property(stc => stc.Content)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(stc => stc.CreatedAt)
                .IsRequired();

            builder.HasOne(stc => stc.SupportTicket)
                .WithMany(st => st.Comments)
                .HasForeignKey(stc => stc.SupportTicketId);
        }
    }
}
