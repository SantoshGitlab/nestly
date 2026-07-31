using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Renders a trigger event + channel into a ready-to-send message with
/// <c>{{Variable}}</c> placeholders substituted (SRS 12.17, task 87b).
/// Template *content* is admin-managed (tasks 126a-d, SRS 12.17) - the
/// implementation reads from the <see cref="INotificationTemplateRepository"/>-backed
/// store rather than a fixed built-in set, cached for the read-heavy path
/// every notification dispatch takes.
/// </summary>
public interface INotificationTemplateRenderer
{
    /// <exception cref="InvalidOperationException">No active template is stored for this event/channel combination.</exception>
    Task<RenderedNotification> RenderAsync(NotificationEventType eventType, NotificationChannel channel, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);

    /// <summary>Whether an active template exists for this combination, without throwing - callers that dispatch across multiple channels use this to skip unsupported ones gracefully.</summary>
    Task<bool> SupportsChannelAsync(NotificationEventType eventType, NotificationChannel channel, CancellationToken cancellationToken = default);
}
