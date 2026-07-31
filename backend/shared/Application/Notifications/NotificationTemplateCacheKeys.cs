namespace Nestly.Application.Notifications;

/// <summary>
/// The <c>IMemoryCache</c> key backing the active notification-template set
/// (SRS 12.17, tasks 126a-d). <c>NotificationTemplateRenderer</c> populates it
/// on first read after a cache miss; <c>NotificationTemplateManagementService</c>
/// evicts it after every write (create/update/activate/deactivate) so an
/// admin's edit is reflected on the very next dispatch rather than waiting out
/// the fallback expiration. A single constant shared between the two keeps
/// them from silently drifting apart into two different cache keys.
/// </summary>
public static class NotificationTemplateCacheKeys
{
    public const string ActiveTemplates = "notification-templates:active";
}
