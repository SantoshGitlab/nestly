using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_message");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ThreadId).IsRequired();
        builder.HasOne<ChatThread>()
            .WithMany()
            .HasForeignKey(x => x.ThreadId)
            .OnDelete(DeleteBehavior.Restrict);

        // Denormalized from the parent thread (see ChatMessage's doc comment)
        // so the notification/hub-broadcast handlers never need a second
        // round trip just to learn what the message is about.
        builder.Property(x => x.ContextType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ContextId).IsRequired();

        builder.Property(x => x.SenderId).IsRequired();
        builder.Property(x => x.SenderType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.SentAtUtc).IsRequired();
        builder.Property(x => x.ReadAtUtc);

        // Paginated history, oldest/newest by thread (task 191).
        builder.HasIndex(x => new { x.ThreadId, x.SentAtUtc });

        // Bulk mark-read scans "this thread's messages not sent by the
        // reader, not yet read" - see IChatMessageRepository.MarkThreadReadAsync.
        builder.HasIndex(x => new { x.ThreadId, x.ReadAtUtc });
    }
}
