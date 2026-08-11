namespace Nestly.Application.Catalog;

/// <summary>Admin view of a service group - an optional section header for a subset of a category's services (e.g. "Repair &amp; gas refill" under "AC").</summary>
public sealed record ServiceGroupAdminResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    bool IsActive,
    int SortOrder);

/// <summary>Admin create request for a service group, mapped to a category at creation (mirrors <see cref="ServiceAddOnGroupCreateRequest"/>'s flat, top-level shape).</summary>
public sealed record ServiceGroupCreateRequest(
    Guid CategoryId,
    string Name,
    int SortOrder);

/// <summary>
/// Admin update request for a service group, including re-mapping it to a
/// different category. Covers every editable field except
/// <see cref="ServiceGroup.IsActive"/>, toggled through its own endpoint so
/// the change is audited as its own distinct action (same convention as
/// <see cref="ServiceVariantUpdateRequest"/>).
/// </summary>
public sealed record ServiceGroupUpdateRequest(
    Guid CategoryId,
    string Name,
    int SortOrder);
