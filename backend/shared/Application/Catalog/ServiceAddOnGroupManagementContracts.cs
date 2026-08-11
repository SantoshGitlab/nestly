namespace Nestly.Application.Catalog;

/// <summary>
/// Admin view of an add-on group (Phase 3 catalog redesign).
/// <see cref="SelectionType"/> is the enum's string name ("Single"/"Multiple"),
/// not the C# enum type - no <c>JsonStringEnumConverter</c> is registered
/// app-wide, so an enum-typed field here would serialize as its numeric
/// ordinal instead (same convention as <see cref="ServiceAdminResponse.PricingType"/>).
/// </summary>
public sealed record ServiceAddOnGroupAdminResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string Name,
    string SelectionType,
    int MinSelect,
    int? MaxSelect,
    int SortOrder);

/// <summary>Admin create request for an add-on group, mapped to a service at creation (mirrors <see cref="ServiceAddOnCreateRequest"/>'s flat, top-level shape).</summary>
public sealed record ServiceAddOnGroupCreateRequest(
    Guid ServiceId,
    string Name,
    string SelectionType,
    int MinSelect,
    int? MaxSelect,
    int SortOrder);

/// <summary>
/// Admin update request for an add-on group, including re-mapping it to a
/// different service (same convention as <see cref="ServiceAddOnUpdateRequest"/>).
/// </summary>
public sealed record ServiceAddOnGroupUpdateRequest(
    Guid ServiceId,
    string Name,
    string SelectionType,
    int MinSelect,
    int? MaxSelect,
    int SortOrder);
