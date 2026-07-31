using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An admin-editable notification template (SRS 12.17, tasks 126a-d): one row
/// per (<see cref="EventType"/>, <see cref="Channel"/>) combination that
/// <c>NotificationTemplateRenderer</c> looks up and substitutes
/// <c>{{Variable}}</c> placeholders into at dispatch time. Replaces the fixed
/// built-in dictionary the renderer used before Phase 6 - see its own doc
/// comment for how the two eras relate. OTP is deliberately absent, same as
/// <see cref="NotificationEventType"/>'s own doc comment explains - it sends
/// through <see cref="INotificationProvider"/> directly and never reaches
/// this renderer.
/// </summary>
/// <remarks>
/// <see cref="EventType"/>, <see cref="Channel"/> and <see cref="TemplateKey"/>
/// are fixed at creation, mirroring <see cref="Coupon.Code"/>'s immutability -
/// the (EventType, Channel) pair is the lookup key every dispatch depends on,
/// so letting it drift under an in-place edit would silently break the very
/// combination a caller resolved before the edit. Deactivating and creating a
/// fresh row is how an admin "retires" a combination instead.
/// </remarks>
public class NotificationTemplate : Entity<Guid>
{
    public NotificationEventType EventType { get; private set; }

    public NotificationChannel Channel { get; private set; }

    /// <summary>Human-readable identifier stored alongside every dispatched <c>NotificationEvent</c>, e.g. "welcome_sms". Unique, immutable.</summary>
    public string TemplateKey { get; private set; } = string.Empty;

    /// <summary>
    /// Email subject / push notification title. Required for Email and Push
    /// (see <c>NotificationDispatchService.DispatchChannelAsync</c>, which
    /// uses this as the push title), forbidden for Sms - Sms has no subject
    /// line.
    /// </summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the renderer treats this row as usable. A dispatch attempt
    /// against an inactive combination behaves exactly like a missing one -
    /// <c>NotificationDispatchService</c> already handles that gracefully by
    /// logging a "no_template" failure rather than throwing.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Admin who last changed this row; null if never changed since creation/seeding.</summary>
    public Guid? UpdatedByAdminUserId { get; private set; }

    protected NotificationTemplate() { }

    public NotificationTemplate(
        Guid id,
        NotificationEventType eventType,
        NotificationChannel channel,
        string templateKey,
        string? subject,
        string body)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ValidateSubject(channel, subject);

        EventType = eventType;
        Channel = channel;
        TemplateKey = templateKey.Trim();
        Subject = subject;
        Body = body;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Edits the subject/body content (SRS 12.17.2). <see cref="EventType"/>/<see cref="Channel"/>/<see cref="TemplateKey"/> are immutable - see the type's own doc comment.</summary>
    public void Update(string? subject, string body, Guid? updatedByAdminUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ValidateSubject(Channel, subject);

        Subject = subject;
        Body = body;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public void Activate(Guid? updatedByAdminUserId)
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public void Deactivate(Guid? updatedByAdminUserId)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void ValidateSubject(NotificationChannel channel, string? subject)
    {
        if (channel == NotificationChannel.Sms && !string.IsNullOrEmpty(subject))
        {
            throw new ArgumentException("An SMS template cannot have a subject.", nameof(subject));
        }

        if (channel != NotificationChannel.Sms && string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Email and push templates require a subject.", nameof(subject));
        }
    }
}
