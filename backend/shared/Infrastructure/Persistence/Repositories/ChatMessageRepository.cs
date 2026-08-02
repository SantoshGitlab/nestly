using Microsoft.EntityFrameworkCore;
using Nestly.Application.Chat;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly NestlyDbContext _context;

    public ChatMessageRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatMessage message)
    {
        await _context.ChatMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public Task<ChatMessage?> GetByIdAsync(Guid id) =>
        _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == id);

    public async Task<(IReadOnlyList<ChatMessage> Messages, int TotalCount)> ListByThreadAsync(Guid threadId, int page, int pageSize)
    {
        var query = _context.ChatMessages.Where(m => m.ThreadId == threadId);

        int totalCount = await query.CountAsync();
        var messages = await query
            .OrderBy(m => m.SentAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (messages, totalCount);
    }

    public Task<int> MarkThreadReadAsync(Guid threadId, Guid readerId, DateTime readAtUtc) =>
        _context.ChatMessages
            .Where(m => m.ThreadId == threadId && m.SenderId != readerId && m.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAtUtc, readAtUtc));
}
