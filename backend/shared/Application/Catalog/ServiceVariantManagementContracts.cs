namespace Nestly.Application.Catalog;

/// <summary>Admin view of a service variant (Phase 3 catalog redesign, SRS 12.6.3 extension).</summary>
public sealed record ServiceVariantAdminResponse(
    Guid Id,
    Guid ServiceId,
    string Name,
    decimal Price,
    int DurationMinutes,
    string? InclusionsOverride,
    bool IsActive,
    int SortOrder);

/// <summary>Admin create request for a variant, scoped to a service by route (mirrors <c>ServiceMediaCreateRequest</c>'s nested-under-service shape).</summary>
public sealed record ServiceVariantCreateRequest(
    string Name,
    decimal Price,
    int DurationMinutes,
    string? InclusionsOverride,
    int SortOrder);

/// <summary>
/// Admin update request for a variant, covering every editable field except
/// <see cref="ServiceVariant.IsActive"/>, toggled through its own endpoint so
/// the change is audited as its own distinct action (same convention as
/// <see cref="ServiceUpdateRequest"/>).
/// </summary>
public sealed record ServiceVariantUpdateRequest(
    string Name,
    decimal Price,
    int DurationMinutes,
    string? InclusionsOverride,
    int SortOrder);
