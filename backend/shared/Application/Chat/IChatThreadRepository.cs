using Nestly.Domain;

namespace Nestly.Application.Chat;

public interface IChatThreadRepository
{
    Task AddAsync(ChatThread thread);

    Task<ChatThread?> GetByIdAsync(Guid id);

    /// <summary>Looks up the single thread for a context, if one has been created yet (task 191's get-or-create).</summary>
    Task<ChatThread?> GetByContextAsync(ChatContextType contextType, Guid contextId);

    /// <summary>Persists <see cref="ChatThread.TouchLastMessage"/>'s recency bump after a new message (task 191).</summary>
    Task UpdateAsync(ChatThread thread);

    /// <summary>Every thread across every customer, most recent first (admin support-console inbox) - unlike <see cref="GetByContextAsync"/>, not scoped to one context.</summary>
    Task<(IReadOnlyList<ChatThread> Threads, int TotalCount)> ListAsync(int page, int pageSize);
}
