using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Seed;

namespace Nestly.Catalog.Tests;

/// <summary>
/// In-memory <see cref="INotificationTemplateRepository"/> fake seeded with
/// the same default rows a fresh database's <c>AddNotificationTemplateManagement</c>
/// migration seeds (see <see cref="NotificationTemplateSeedData"/>) - lets
/// tests build a real <c>NotificationTemplateRenderer</c> without a database,
/// matching this test project's existing no-mocking-library convention (see
/// <see cref="InMemoryCacheService"/>).
/// </summary>
public sealed class FakeNotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly List<NotificationTemplate> _templates;

    public FakeNotificationTemplateRepository()
    {
        _templates = NotificationTemplateSeedData.BuildDefaults()
            .Select(row => new NotificationTemplate(row.Id, row.EventType, row.Channel, row.TemplateKey, row.Subject, row.Body))
            .ToList();
    }

    public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

    public Task<bool> ExistsForEventAndChannelAsync(NotificationEventType eventType, NotificationChannel channel, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_templates.Any(t => t.EventType == eventType && t.Channel == channel && (excludeId == null || t.Id != excludeId)));

    public Task<IReadOnlyList<NotificationTemplate>> ListAsync(NotificationChannel? channel, NotificationEventType? eventType, bool? isActive, CancellationToken cancellationToken = default)
    {
        IEnumerable<NotificationTemplate> query = _templates;

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

        return Task.FromResult<IReadOnlyList<NotificationTemplate>>(query.ToList());
    }

    public Task<IReadOnlyList<NotificationTemplate>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificationTemplate>>(_templates.Where(t => t.IsActive).ToList());

    public Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        _templates.Add(template);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
