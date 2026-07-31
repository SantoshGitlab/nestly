using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A site-level FAQ entry (SRS 12.16.1 "FAQ entries", tasks 124c/124d) - a
/// question/answer pair with its own draft/publish workflow, sort order,
/// placement, and optional publish window. Distinct from
/// <see cref="ServiceFaq"/> (task 40e), which is scoped to a single
/// service's product page - this is general site content (e.g. a help
/// center or checkout FAQ block). Named "CmsFaq" specifically so it is never
/// confused with that per-service entity, per this task's brief.
/// </summary>
public class CmsFaq : Entity<Guid>
{
    public string Question { get; private set; } = string.Empty;

    public string Answer { get; private set; } = string.Empty;

    public CmsPlacement Placement { get; private set; }

    public int SortOrder { get; private set; }

    public CmsContentStatus Status { get; private set; }

    public DateTime? PublishStartUtc { get; private set; }

    public DateTime? PublishEndUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    protected CmsFaq() { }

    public CmsFaq(
        Guid id,
        string question,
        string answer,
        CmsPlacement placement,
        int sortOrder,
        CmsContentStatus status,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
        : base(id)
    {
        Validate(question, answer, sortOrder, publishStartUtc, publishEndUtc);

        Question = question.Trim();
        Answer = answer.Trim();
        Placement = placement;
        SortOrder = sortOrder;
        Status = status;
        PublishStartUtc = publishStartUtc;
        PublishEndUtc = publishEndUtc;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Update(
        string question,
        string answer,
        CmsPlacement placement,
        int sortOrder,
        DateTime? publishStartUtc,
        DateTime? publishEndUtc)
    {
        Validate(question, answer, sortOrder, publishStartUtc, publishEndUtc);

        Question = question.Trim();
        Answer = answer.Trim();
        Placement = placement;
        SortOrder = sortOrder;
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

    /// <summary>Whether this FAQ is currently visible to end users: published, and within its optional publish window (SRS 12.16.2, task 124d).</summary>
    public bool IsLive(DateTime nowUtc) =>
        Status == CmsContentStatus.Published
        && (!PublishStartUtc.HasValue || nowUtc >= PublishStartUtc.Value)
        && (!PublishEndUtc.HasValue || nowUtc <= PublishEndUtc.Value);

    private static void Validate(string question, string answer, int sortOrder, DateTime? publishStartUtc, DateTime? publishEndUtc)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("FAQ question is required.", nameof(question));
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ArgumentException("FAQ answer is required.", nameof(answer));
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
