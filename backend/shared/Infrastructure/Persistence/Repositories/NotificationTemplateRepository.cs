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
        var query = KnownEventTypeOnly();

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
        await KnownEventTypeOnly()
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// <see cref="NotificationEventType"/> is stored as its string name (see that type's
    /// "APPENDED, NEVER INSERTED" remark on why it can't be an int) - a value the enum no
    /// longer defines, left behind by a renamed member or hand-inserted test data, makes EF's
    /// string-to-enum converter throw the instant it materializes that row. Unfiltered, that
    /// takes down every caller of this repository - which includes every notification dispatch
    /// in the app (<c>NotificationTemplateRenderer.GetActiveTemplatesAsync</c> calls
    /// <see cref="ListActiveAsync"/> for every event) and the admin template screen itself
    /// (<c>ListAsync</c>), so an operator couldn't even see the offending row to fix it.
    /// Restricting to recognized names in SQL, before EF converts anything, keeps one stale row
    /// from being a single point of failure for the whole notification pipeline.
    /// </summary>
    /// <remarks>
    /// This was raw SQL using PostgreSQL's <c>= ANY(array)</c> until 2026-08-08. <c>ANY</c> is
    /// provider-specific, so the SQLite-backed test suite (docs/TESTING.md) failed outright on
    /// "no such function: ANY" - which made every caller of this repository untestable, the
    /// notification pipeline included. <c>Contains</c> over the enum's own values expresses the
    /// same filter in LINQ: EF applies this property's string value converter
    /// (<c>NotificationTemplateConfiguration</c>) to each element and emits a standard
    /// <c>event_type IN (...)</c> that both providers run identically. It still filters in SQL,
    /// before EF materializes - which is the whole point - and needs no raw SQL to do it.
    /// </remarks>
    private IQueryable<NotificationTemplate> KnownEventTypeOnly()
    {
        var validEventTypes = Enum.GetValues<NotificationEventType>();

        return _context.Set<NotificationTemplate>()
            .Where(t => validEventTypes.Contains(t.EventType));
    }

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
