using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.ToTable("chat_thread");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContextType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ContextId).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastMessageAtUtc).IsRequired();

        // At most one thread per (context_type, context_id) - the invariant
        // the service layer's get-or-create (task 191) relies on.
        builder.HasIndex(x => new { x.ContextType, x.ContextId }).IsUnique();
    }
}
