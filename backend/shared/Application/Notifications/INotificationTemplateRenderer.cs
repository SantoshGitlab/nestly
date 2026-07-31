using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Renders a trigger event + channel into a ready-to-send message with
/// <c>{{Variable}}</c> placeholders substituted (SRS 12.17, task 87b).
/// Template *content* management (admin CRUD, versioning) is Phase 6's job
/// (SRS 12.17) - this only covers the rendering engine itself, with a fixed
/// built-in template set standing in for the not-yet-built admin-managed one.
/// </summary>
public interface INotificationTemplateRenderer
{
    /// <exception cref="InvalidOperationException">No template is registered for this event/channel combination.</exception>
    RenderedNotification Render(NotificationEventType eventType, NotificationChannel channel, IReadOnlyDictionary<string, string> variables);

    /// <summary>Whether a template exists for this combination, without throwing - callers that dispatch across multiple channels use this to skip unsupported ones gracefully.</summary>
    bool SupportsChannel(NotificationEventType eventType, NotificationChannel channel);
}
