using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A promotional banner (SRS 12.16.1 "Home banners / Category banners /
/// Promotional blocks", tasks 124b/124d/124f). Image/link, placement,
/// ordering, and an optional publish window are all first-class fields per
/// SRS 12.16.2 ("draft/publish status", "publish start/end date optional",
/// "sort order"). References <see cref="CmsMedia"/> rather than carrying its
/// own bare URL so its image always comes from the managed media library
/// (task 124e).
/// </summary>
public class Banner : Entity<Guid>
{
    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional supporting line shown beneath <see cref="Title"/> on the storefront banner (SRS 11.1.3 "content shall be admin-configurable"). Null when the banner is a headline only.</summary>
    public string? Subtitle { get; private set; }

    public Guid MediaId { get; private set; }

    public string? LinkUrl { get; private set; }

    public CmsPlacement Placement { get; private set; }

    /// <summary>Required exactly when <see cref="Placement"/> is <see cref="CmsPlacement.CategoryPage"/> - checked here structurally; actual category existence is validated in <c>BannerService</c>, which has repository access this entity does not.</summary>
    public Guid? CategoryId { get; private set; }

    public int SortOrder { get; private set; }

    public CmsContentStatus Status { get; private set; }

    public DateTime? PublishStartUtc { get; private set; }

    public DateTime? PublishEndUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    protected Banner() { }

    public Banner(
        Guid id,
        string title,
        string? subtitle,
        Guid mediaId,
        string? linkUrl,
        CmsPlacement placement,
        Guid? categoryId,
        int sortOrder,
        CmsContentStatus status,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
        : base(id)
    {
        Validate(title, subtitle, mediaId, placement, categoryId, sortOrder, publishStartUtc, publishEndUtc);

        Title = title.Trim();
        Subtitle = NormalizeSubtitle(subtitle);
        MediaId = mediaId;
        LinkUrl = linkUrl;
        Placement = placement;
        CategoryId = categoryId;
        SortOrder = sortOrder;
        Status = status;
        PublishStartUtc = publishStartUtc;
        PublishEndUtc = publishEndUtc;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Edits every mutable field. <see cref="Status"/> is deliberately excluded - see <see cref="Publish"/>/<see cref="Unpublish"/>.</summary>
    public void Update(
        string title,
        string? subtitle,
        Guid mediaId,
        string? linkUrl,
        CmsPlacement placement,
        Guid? categoryId,
        int sortOrder,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
    {
        Validate(title, subtitle, mediaId, placement, categoryId, sortOrder, publishStartUtc, publishEndUtc);

        Title = title.Trim();
        Subtitle = NormalizeSubtitle(subtitle);
        MediaId = mediaId;
        LinkUrl = linkUrl;
        Placement = placement;
        CategoryId = categoryId;
        SortOrder = sortOrder;
        PublishStartUtc = publishStartUtc;
        PublishEndUtc = publishEndUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Moves a draft banner live, or re-publishes one already published (SRS 12.16.2 "draft/publish status").</summary>
    public void Publish()
    {
        Status = CmsContentStatus.Published;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Pulls a banner back to draft without deleting it.</summary>
    public void Unpublish()
    {
        Status = CmsContentStatus.Draft;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Whether this banner is currently visible to end users: published, and within its optional publish window (SRS 12.16.2, task 124d).</summary>
    public bool IsLive(DateTime nowUtc) =>
        Status == CmsContentStatus.Published
        && (!PublishStartUtc.HasValue || nowUtc >= PublishStartUtc.Value)
        && (!PublishEndUtc.HasValue || nowUtc <= PublishEndUtc.Value);

    /// <summary>Blank/whitespace subtitle collapses to null so "no subtitle" has one representation, not two.</summary>
    private static string? NormalizeSubtitle(string? subtitle) =>
        string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();

    private static void Validate(
        string title,
        string? subtitle,
        Guid mediaId,
        CmsPlacement placement,
        Guid? categoryId,
        int sortOrder,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Banner title is required.", nameof(title));
        }

        if (subtitle is not null && subtitle.Trim().Length > 300)
        {
            throw new ArgumentException("Banner subtitle must be 300 characters or fewer.", nameof(subtitle));
        }

        if (mediaId == Guid.Empty)
        {
            throw new ArgumentException("Banner media is required.", nameof(mediaId));
        }

        if (placement == CmsPlacement.CategoryPage && !categoryId.HasValue)
        {
            throw new ArgumentException("A category is required when placement is CategoryPage.", nameof(categoryId));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        if (publishStartUtc.HasValue && publishEndUtc.HasValue && publishEndUtc.Value <= publishStartUtc.Value)
        {
            throw new ArgumentException("Publish end date must be after the publish start date.", nameof(publishEndUtc));
        }
    }
}
