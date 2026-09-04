namespace Nestly.Domain;

/// <summary>
/// The kind of asset a <see cref="CmsMedia"/> row holds. <see cref="Banner"/>
/// and any other CMS content that renders a media asset needs this to decide
/// how to render it (an <c>&lt;img&gt;</c> versus a looping, muted
/// <c>&lt;video&gt;</c>) - <see cref="CmsMedia.Url"/> alone does not reveal
/// that. AdminApi has no JsonStringEnumConverter registered (see
/// <see cref="CmsPlacement"/>'s frontend mirror), so this crosses the wire as
/// its ordinal; keep declaration order in sync with
/// frontend/admin-web/src/lib/cms-types.ts and
/// frontend/customer-web/src/lib/types.ts.
/// </summary>
public enum CmsMediaType
{
    Image,
    Video,
}
