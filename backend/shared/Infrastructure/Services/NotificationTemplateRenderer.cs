using Microsoft.Extensions.Caching.Memory;
using Nestly.Application.Notifications;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin-managed, DB-backed notification template lookup (SRS 12.17, task
/// 87b + tasks 126a-d). This used to hold a fixed, built-in
/// <c>Dictionary&lt;(EventType, Channel), TemplateDefinition&gt;</c> - the
/// admin CRUD in <c>NotificationTemplateManagementService</c> now owns that
/// content, so this class is reduced to the read path: load every active row
/// once via <see cref="INotificationTemplateRepository.ListActiveAsync"/>,
/// cache it (this is on the hot path of every notification dispatch), and
/// substitute placeholders exactly as before.
/// </summary>
/// <remarks>
/// Scoped, not singleton - it now depends on <see cref="INotificationTemplateRepository"/>,
/// which wraps a scoped <c>NestlyDbContext</c>. The cached dictionary itself
/// lives in <see cref="IMemoryCache"/> (a singleton), so this still only
/// costs one database round trip per cache expiry/invalidation, not per
/// request.
/// </remarks>
public sealed class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    /// <summary>
    /// Defensive upper bound in case <see cref="NotificationTemplateManagementService"/>'s
    /// explicit cache eviction is ever missed (e.g. a direct database edit
    /// bypassing the admin API) - an admin's edit is never invisible for
    /// longer than this.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly INotificationTemplateRepository _repository;
    private readonly IMemoryCache _cache;

    public NotificationTemplateRenderer(INotificationTemplateRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<RenderedNotification> RenderAsync(NotificationEventType eventType, NotificationChannel channel, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        var templates = await GetActiveTemplatesAsync(cancellationToken);
        if (!templates.TryGetValue((eventType, channel), out var template))
        {
            throw new InvalidOperationException($"No notification template registered for {eventType}/{channel}.");
        }

        return new RenderedNotification(
            template.TemplateKey,
            NotificationPlaceholderSubstitution.Substitute(template.Subject, variables),
            NotificationPlaceholderSubstitution.Substitute(template.Body, variables)!);
    }

    public async Task<bool> SupportsChannelAsync(NotificationEventType eventType, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var templates = await GetActiveTemplatesAsync(cancellationToken);
        return templates.ContainsKey((eventType, channel));
    }

    private Task<IReadOnlyDictionary<(NotificationEventType, NotificationChannel), NotificationTemplate>> GetActiveTemplatesAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(NotificationTemplateCacheKeys.ActiveTemplates, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var rows = await _repository.ListActiveAsync(cancellationToken);
            IReadOnlyDictionary<(NotificationEventType, NotificationChannel), NotificationTemplate> byKey =
                rows.ToDictionary(t => (t.EventType, t.Channel), t => t);
            return byKey;
        })!;
}
