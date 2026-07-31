using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A static site page (SRS 12.16.1 "About / policy pages", "SEO content for
/// key public pages", tasks 124a/124d/124f) - title/slug/body plus SEO
/// fields, a draft/publish workflow, an optional publish window, and a
/// placement (<see cref="CmsPlacement.Footer"/> covers "Footer links"
/// without a separate entity).
/// </summary>
public class CmsPage : Entity<Guid>
{
    public string Title { get; private set; } = string.Empty;

    /// <summary>URL-safe identifier the public site resolves the page by. Normalized to lowercase; uniqueness is enforced at the database layer and re-checked in <c>CmsPageService</c> before every create/update.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? SeoTitle { get; private set; }

    public string? SeoDescription { get; private set; }

    public string? SeoKeywords { get; private set; }

    public CmsPlacement Placement { get; private set; }

    public CmsContentStatus Status { get; private set; }

    public DateTime? PublishStartUtc { get; private set; }

    public DateTime? PublishEndUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    protected CmsPage() { }

    public CmsPage(
        Guid id,
        string title,
        string slug,
        string body,
        string? seoTitle,
        string? seoDescription,
        string? seoKeywords,
        CmsPlacement placement,
        CmsContentStatus status,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
        : base(id)
    {
        Validate(title, slug, body, publishStartUtc, publishEndUtc);

        Title = title.Trim();
        Slug = NormalizeSlug(slug);
        Body = body;
        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
        SeoKeywords = seoKeywords;
        Placement = placement;
        Status = status;
        PublishStartUtc = publishStartUtc;
        PublishEndUtc = publishEndUtc;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Update(
        string title,
        string slug,
        string body,
        string? seoTitle,
        string? seoDescription,
        string? seoKeywords,
        CmsPlacement placement,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
    {
        Validate(title, slug, body, publishStartUtc, publishEndUtc);

        Title = title.Trim();
        Slug = NormalizeSlug(slug);
        Body = body;
        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
        SeoKeywords = seoKeywords;
        Placement = placement;
        PublishStartUtc = publishStartUtc;
        PublishEndUtc = publishEndUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = CmsContentStatus.Published;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        Status = CmsContentStatus.Draft;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Whether this page is currently visible to end users: published, and within its optional publish window (SRS 12.16.2, task 124d).</summary>
    public bool IsLive(DateTime nowUtc) =>
        Status == CmsContentStatus.Published
        && (!PublishStartUtc.HasValue || nowUtc >= PublishStartUtc.Value)
        && (!PublishEndUtc.HasValue || nowUtc <= PublishEndUtc.Value);

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static void Validate(string title, string slug, string body, DateTime? publishStartUtc, DateTime? publishEndUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Page title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Page slug is required.", nameof(slug));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Page body is required.", nameof(body));
        }

        if (publishStartUtc.HasValue && publishEndUtc.HasValue && publishEndUtc.Value <= publishStartUtc.Value)
        {
            throw new ArgumentException("Publish end date must be after the publish start date.", nameof(publishEndUtc));
        }
    }
}
