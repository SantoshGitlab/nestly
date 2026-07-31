using Microsoft.EntityFrameworkCore;
using Nestly.Application.Notifications;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly NestlyDbContext _context;

    public NotificationTemplateRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<NotificationTemplate>().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsForEventAndChannelAsync(NotificationEventType eventType, NotificationChannel channel, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _context.Set<NotificationTemplate>().AnyAsync(
            t => t.EventType == eventType && t.Channel == channel && (excludeId == null || t.Id != excludeId),
            cancellationToken);

    public async Task<IReadOnlyList<NotificationTemplate>> ListAsync(NotificationChannel? channel, NotificationEventType? eventType, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<NotificationTemplate>().AsQueryable();

        if (channel.HasValue)
        {
            query = query.Where(t => t.Channel == channel.Value);
        }

        if (eventType.HasValue)
        {
            query = query.Where(t => t.EventType == eventType.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(t => t.EventType)
            .ThenBy(t => t.Channel)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationTemplate>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<NotificationTemplate>()
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        await _context.Set<NotificationTemplate>().AddAsync(template, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        _context.Set<NotificationTemplate>().Update(template);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
