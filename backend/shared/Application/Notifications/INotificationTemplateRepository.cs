using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Persistence for admin-managed notification templates (SRS 12.17, tasks
/// 126a-d). Shared by two very different callers: <c>NotificationTemplateRenderer</c>
/// (the read-only, cached, dispatch-critical lookup every notification send
/// goes through) and <c>NotificationTemplateManagementService</c> (the admin
/// CRUD behind it) - the same dual-use split <c>ICouponRepository</c> serves
/// for <c>CouponService</c>/<c>CouponManagementService</c>.
/// </summary>
public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForEventAndChannelAsync(NotificationEventType eventType, NotificationChannel channel, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationTemplate>> ListAsync(NotificationChannel? channel, NotificationEventType? eventType, bool? isActive, CancellationToken cancellationToken = default);

    /// <summary>Every active row, keyed for the renderer's cache - see <c>NotificationTemplateCacheKeys</c>.</summary>
    Task<IReadOnlyList<NotificationTemplate>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default);

    Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
}
