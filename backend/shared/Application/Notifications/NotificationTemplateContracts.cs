using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>Full template detail for the admin list/edit screens (SRS 12.17.2's field set, tasks 126a-d).</summary>
public sealed record NotificationTemplateResponse(
    Guid Id,
    NotificationEventType EventType,
    NotificationChannel Channel,
    string TemplateKey,
    string? Subject,
    string Body,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByAdminUserId);

/// <summary>
/// Admin request to create a template for a not-yet-covered (EventType,
/// Channel) combination (SRS 12.17.1-2). Rejected with a conflict if that
/// combination already has a row - see <see cref="NotificationTemplate"/>'s
/// doc comment for why the pair is immutable once created.
/// </summary>
public sealed record NotificationTemplateCreateRequest(
    NotificationEventType EventType,
    NotificationChannel Channel,
    string TemplateKey,
    string? Subject,
    string Body);

/// <summary>Admin request to edit an existing template's content (SRS 12.17.2). Omits EventType/Channel/TemplateKey - see <see cref="NotificationTemplate.Update"/>.</summary>
public sealed record NotificationTemplateUpdateRequest(string? Subject, string Body);

/// <summary>Renders a saved template's subject/body against sample values (SRS 12.17.2 "Preview/test capability"), without sending anything or persisting the render.</summary>
public sealed record NotificationTemplatePreviewRequest(IReadOnlyDictionary<string, string> SampleVariables);

/// <summary>
/// Ad-hoc preview of draft subject/body text against sample values, for the
/// template editor to render as-you-type before the admin saves (task 127) -
/// does not require a saved <see cref="NotificationTemplate"/> to already
/// exist.
/// </summary>
public sealed record NotificationTemplateAdHocPreviewRequest(
    NotificationChannel Channel,
    string? Subject,
    string Body,
    IReadOnlyDictionary<string, string> SampleVariables);

public sealed record NotificationTemplatePreviewResponse(string? Subject, string Body);
