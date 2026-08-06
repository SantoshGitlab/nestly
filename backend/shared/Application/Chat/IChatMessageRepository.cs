using Nestly.Domain;

namespace Nestly.Application.Chat;

public interface IChatMessageRepository
{
    /// <summary>Messages are append-only - there is deliberately no Update/Delete method (see ChatMessage's doc comment), mirroring IWalletLedgerRepository.</summary>
    Task AddAsync(ChatMessage message);

    Task<ChatMessage?> GetByIdAsync(Guid id);

    /// <summary>Paginated history, oldest first (task 191) - a chat thread reads top-to-bottom like the conversation happened.</summary>
    Task<(IReadOnlyList<ChatMessage> Messages, int TotalCount)> ListByThreadAsync(Guid threadId, int page, int pageSize);

    /// <summary>
    /// Bulk-sets ReadAtUtc (to <paramref name="readAtUtc"/>) on every message in
    /// <paramref name="threadId"/> not sent by <paramref name="readerId"/> that
    /// has no read receipt yet. An EF Core <c>ExecuteUpdateAsync</c>
    /// column-only update, not a load-each-entity-and-mutate loop - see
    /// ChatMessage's doc comment for why. Returns the number of rows touched.
    /// </summary>
    Task<int> MarkThreadReadAsync(Guid threadId, Guid readerId, DateTime readAtUtc);

    /// <summary>
    /// Per-thread count of not-yet-admin-read messages (sender not Admin,
    /// no read receipt), batched across a page of threads for the admin chat
    /// inbox's unread badge - one GROUP BY instead of one COUNT per row.
    /// Threads with no unread messages are simply absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountUnreadByThreadIdsAsync(IReadOnlyCollection<Guid> threadIds);
}
