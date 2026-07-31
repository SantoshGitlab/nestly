using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SupportTicketCommentConfiguration : IEntityTypeConfiguration<SupportTicketComment>
{
    public void Configure(EntityTypeBuilder<SupportTicketComment> builder)
    {
        builder.ToTable("support_ticket_comment");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupportTicketId).IsRequired();
        builder.Property(x => x.AuthorType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Comment).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.SupportTicketId);
    }
}
