namespace Nestly.Application.Catalog;

/// <summary>Admin view of an add-on mapped to a service (SRS 12.7.2).</summary>
public sealed record ServiceAddOnAdminResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    int SortOrder,
    bool IsQuantityAllowed,
    bool IsMandatory);

/// <summary>Admin create request for an add-on, mapped to a service at creation (SRS 12.7.1).</summary>
public sealed record ServiceAddOnCreateRequest(
    Guid ServiceId,
    string Name,
    string? Description,
    decimal Price,
    int SortOrder,
    bool IsQuantityAllowed,
    bool IsMandatory);

/// <summary>
/// Admin update request for an add-on, including re-mapping it to a
/// different service - covers every editable field (SRS 12.7.2) except
/// <see cref="ServiceAddOn.IsActive"/>, toggled through its own endpoint so
/// the change is audited as its own distinct action.
/// </summary>
public sealed record ServiceAddOnUpdateRequest(
    Guid ServiceId,
    string Name,
    string? Description,
    decimal Price,
    int SortOrder,
    bool IsQuantityAllowed,
    bool IsMandatory);
