using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin CRUD, preview and change-audited editing over notification templates
/// (SRS 12.17, tasks 126a-d). Every mutating rule is delegated to
/// <see cref="NotificationTemplate"/>'s own constructor/<c>Update</c> - this
/// service is the I/O-dependent half (uniqueness checks, persistence,
/// audit, cache invalidation) the entity has no business doing itself, the
/// same split <c>CategoryManagementService</c> draws for categories.
/// </summary>
public class NotificationTemplateManagementService : INotificationTemplateManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationTemplateRepository _repository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IAuditContextProvider _auditContextProvider;
    private readonly IMemoryCache _cache;

    public NotificationTemplateManagementService(
        INotificationTemplateRepository repository,
        IAuditLogWriter auditLogWriter,
        IAuditContextProvider auditContextProvider,
        IMemoryCache cache)
    {
        _repository = repository;
        _auditLogWriter = auditLogWriter;
        _auditContextProvider = auditContextProvider;
        _cache = cache;
    }

    public async Task<IReadOnlyList<NotificationTemplateResponse>> ListAsync(
        NotificationChannel? channel, NotificationEventType? eventType, bool? isActive, CancellationToken cancellationToken = default)
    {
        var templates = await _repository.ListAsync(channel, eventType, isActive, cancellationToken);
        return templates.Select(ToResponse).ToList();
    }

    public async Task<Result<NotificationTemplateResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        return template is null ? NotFound() : ToResponse(template);
    }

    public async Task<Result<NotificationTemplateResponse>> CreateAsync(NotificationTemplateCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.ExistsForEventAndChannelAsync(request.EventType, request.Channel, cancellationToken: cancellationToken))
        {
            return Error.Conflict("NotificationTemplate.AlreadyExists", "A template already exists for this event and channel - edit it instead of creating a duplicate.");
        }

        NotificationTemplate template;
        try
        {
            template = new NotificationTemplate(Guid.NewGuid(), request.EventType, request.Channel, request.TemplateKey, request.Subject, request.Body);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("NotificationTemplate.InvalidSubject", ex.Message);
        }

        // Staged before AddAsync so its own SaveChangesAsync commits the
        // audit row in the same transaction as the new template
        // (IAuditLogWriter's documented contract), same ordering
        // CategoryManagementService.CreateAsync uses.
        await _auditLogWriter.WriteAsync(new AuditEntry(
            "NotificationTemplate", template.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(template)), cancellationToken);

        await _repository.AddAsync(template, cancellationToken);
        InvalidateCache();

        return ToResponse(template);
    }

    public async Task<Result<NotificationTemplateResponse>> UpdateAsync(Guid id, NotificationTemplateUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        string oldValues = Serialize(template);

        try
        {
            template.Update(request.Subject, request.Body, _auditContextProvider.GetCurrent().ActorId);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("NotificationTemplate.InvalidSubject", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "NotificationTemplate", template.Id.ToString(), "Updated", oldValues, Serialize(template)), cancellationToken);

        await _repository.UpdateAsync(template, cancellationToken);
        InvalidateCache();

        return ToResponse(template);
    }

    public async Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await SetActiveAsync(id, isActive: true, cancellationToken);

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await SetActiveAsync(id, isActive: false, cancellationToken);

    public async Task<Result<NotificationTemplatePreviewResponse>> PreviewAsync(Guid id, NotificationTemplatePreviewRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return new NotificationTemplatePreviewResponse(
            NotificationPlaceholderSubstitution.Substitute(template.Subject, request.SampleVariables),
            NotificationPlaceholderSubstitution.Substitute(template.Body, request.SampleVariables)!);
    }

    public NotificationTemplatePreviewResponse PreviewAdHoc(NotificationTemplateAdHocPreviewRequest request) =>
        new(
            NotificationPlaceholderSubstitution.Substitute(request.Subject, request.SampleVariables),
            NotificationPlaceholderSubstitution.Substitute(request.Body, request.SampleVariables)!);

    private async Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Result.Failure(NotFound());
        }

        Guid? actorId = _auditContextProvider.GetCurrent().ActorId;
        if (isActive) template.Activate(actorId); else template.Deactivate(actorId);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "NotificationTemplate", template.Id.ToString(), isActive ? "Activated" : "Deactivated"), cancellationToken);

        await _repository.UpdateAsync(template, cancellationToken);
        InvalidateCache();

        return Result.Success();
    }

    /// <summary>
    /// Evicts the renderer's cached active-template dictionary so this write
    /// is reflected on the very next dispatch, rather than waiting out
    /// <c>NotificationTemplateRenderer.CacheDuration</c> - see
    /// <see cref="NotificationTemplateCacheKeys"/>'s doc comment.
    /// </summary>
    private void InvalidateCache() => _cache.Remove(NotificationTemplateCacheKeys.ActiveTemplates);

    private static Error NotFound() =>
        Error.NotFound("NotificationTemplate.NotFound", "The specified notification template does not exist.");

    private static string Serialize(NotificationTemplate template) => JsonSerializer.Serialize(ToResponse(template), JsonOptions);

    private static NotificationTemplateResponse ToResponse(NotificationTemplate template) => new(
        template.Id,
        template.EventType,
        template.Channel,
        template.TemplateKey,
        template.Subject,
        template.Body,
        template.IsActive,
        template.CreatedAtUtc,
        template.UpdatedAtUtc,
        template.UpdatedByAdminUserId);
}
