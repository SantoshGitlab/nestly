using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Admin CRUD, preview and change-audited editing over notification templates
/// (SRS 12.17, tasks 126a-d). Distinct from <see cref="INotificationTemplateRenderer"/>,
/// which is the live, cached, dispatch-facing read path this service's writes
/// feed into - the same split <c>ICouponManagementService</c> draws against
/// <c>ICouponService</c>.
/// </summary>
public interface INotificationTemplateManagementService
{
    Task<IReadOnlyList<NotificationTemplateResponse>> ListAsync(
        NotificationChannel? channel, NotificationEventType? eventType, bool? isActive, CancellationToken cancellationToken = default);

    Task<Result<NotificationTemplateResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<NotificationTemplateResponse>> CreateAsync(NotificationTemplateCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<NotificationTemplateResponse>> UpdateAsync(Guid id, NotificationTemplateUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renders a saved template against sample values (task 126b) - a pure render, no dispatch, no persistence.</summary>
    Task<Result<NotificationTemplatePreviewResponse>> PreviewAsync(Guid id, NotificationTemplatePreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renders draft (not-yet-saved) subject/body text against sample values, for the editor's live preview (task 127).</summary>
    NotificationTemplatePreviewResponse PreviewAdHoc(NotificationTemplateAdHocPreviewRequest request);
}
