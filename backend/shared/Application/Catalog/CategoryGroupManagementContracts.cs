namespace Nestly.Application.Catalog;

/// <summary>
/// Admin view of a category group - an optional section header for a subset
/// of a parent category's subcategories (e.g. "Large appliances" under "AC &amp;
/// Appliance Repair"). <see cref="CategoryId"/>/<see cref="CategoryName"/> name
/// the PARENT category whose subcategory listing this group organizes, not
/// a subcategory itself.
/// </summary>
public sealed record CategoryGroupAdminResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    bool IsActive,
    int SortOrder);

/// <summary>Admin create request for a category group, mapped to its parent category at creation (mirrors <see cref="ServiceGroupCreateRequest"/>'s flat, top-level shape).</summary>
public sealed record CategoryGroupCreateRequest(
    Guid CategoryId,
    string Name,
    int SortOrder);

/// <summary>
/// Admin update request for a category group, including re-mapping it to a
/// different parent category. Covers every editable field except
/// <see cref="CategoryGroup.IsActive"/>, toggled through its own endpoint so
/// the change is audited as its own distinct action (same convention as
/// <see cref="ServiceGroupUpdateRequest"/>).
/// </summary>
public sealed record CategoryGroupUpdateRequest(
    Guid CategoryId,
    string Name,
    int SortOrder);
