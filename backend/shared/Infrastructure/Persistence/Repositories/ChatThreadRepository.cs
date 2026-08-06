using Microsoft.EntityFrameworkCore;
using Nestly.Application.Chat;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ChatThreadRepository : IChatThreadRepository
{
    private readonly NestlyDbContext _context;

    public ChatThreadRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatThread thread)
    {
        await _context.ChatThreads.AddAsync(thread);
        await _context.SaveChangesAsync();
    }

    public Task<ChatThread?> GetByIdAsync(Guid id) =>
        _context.ChatThreads.FirstOrDefaultAsync(t => t.Id == id);

    public Task<ChatThread?> GetByContextAsync(ChatContextType contextType, Guid contextId) =>
        _context.ChatThreads.FirstOrDefaultAsync(t => t.ContextType == contextType && t.ContextId == contextId);

    public async Task UpdateAsync(ChatThread thread)
    {
        _context.ChatThreads.Update(thread);
        await _context.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<ChatThread> Threads, int TotalCount)> ListAsync(int page, int pageSize)
    {
        int totalCount = await _context.ChatThreads.CountAsync();

        var threads = await _context.ChatThreads
            .OrderByDescending(t => t.LastMessageAtUtc)
            .ApplyPaging(page, pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (threads, totalCount);
    }
}
