using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Which curated block of the customer home page a <see cref="LandingSelection"/> belongs to.</summary>
public enum LandingSectionType
{
    /// <summary>"New &amp; Trending" - admin-picked sub-categories (a <see cref="Category"/> that has a parent).</summary>
    NewAndTrending = 0,

    /// <summary>"Most Booked Services" - admin-picked bookable <see cref="Service"/>s, shown with price.</summary>
    MostBooked = 1,

    /// <summary>
    /// A category-wise strip: admin-picked <see cref="Service"/>s grouped under
    /// one heading <see cref="Category"/>. Capped at
    /// <see cref="MaxServicesPerCategorySection"/> per heading.
    /// </summary>
    CategorySection = 2
}

/// <summary>
/// One admin-curated entry on the customer home page. Deliberately one table
/// with a <see cref="SectionType"/> discriminator rather than three
/// near-identical tables: every section is "an ordered list of catalog
/// references", and the storage shape, admin screen and query path are shared.
///
/// The nullable <see cref="CategoryId"/>/<see cref="ServiceId"/> pair is only
/// nullable at the schema level - the factory methods below are the sole way
/// to construct one, so an instance can never carry the wrong combination for
/// its section:
/// <list type="bullet">
/// <item><see cref="NewAndTrending"/>: category only.</item>
/// <item><see cref="MostBooked"/>: service only.</item>
/// <item><see cref="CategorySection"/>: both - the category is the section
/// heading, the service is the card under it.</item>
/// </list>
///
/// Curation is intentionally NOT a flag on <see cref="Category"/>/<see cref="Service"/>
/// (they already carry <c>IsFeatured</c>): a boolean cannot express admin
/// ordering, the per-heading cap, or the same service appearing in two
/// sections, and overloading it would make "featured" mean three different
/// things at once.
/// </summary>
public class LandingSelection : Entity<Guid>
{
    /// <summary>Requirement cap for <see cref="LandingSectionType.CategorySection"/>; enforced in the management service, which can count siblings.</summary>
    public const int MaxServicesPerCategorySection = 5;

    public LandingSectionType SectionType { get; private set; }

    /// <summary>The picked sub-category (New &amp; Trending) or the section heading (category strip); null for Most Booked.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>The picked bookable service (Most Booked, category strip); null for New &amp; Trending.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Admin-controlled display order within the section (and, for a category strip, within its heading).</summary>
    public int SortOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    protected LandingSelection() { }

    private LandingSelection(Guid id, LandingSectionType sectionType, Guid? categoryId, Guid? serviceId, int sortOrder)
        : base(id)
    {
        SectionType = sectionType;
        CategoryId = categoryId;
        ServiceId = serviceId;
        SortOrder = sortOrder;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>A "New &amp; Trending" pick: one sub-category, shown as an image card with no price.</summary>
    public static LandingSelection ForNewAndTrending(Guid id, Guid categoryId, int sortOrder) =>
        new(id, LandingSectionType.NewAndTrending, categoryId, serviceId: null, sortOrder);

    /// <summary>A "Most Booked Services" pick: one bookable service, shown with image, title and price.</summary>
    public static LandingSelection ForMostBooked(Guid id, Guid serviceId, int sortOrder) =>
        new(id, LandingSectionType.MostBooked, categoryId: null, serviceId, sortOrder);

    /// <summary>A category-strip pick: one service shown under <paramref name="categoryId"/>'s heading.</summary>
    public static LandingSelection ForCategorySection(Guid id, Guid categoryId, Guid serviceId, int sortOrder) =>
        new(id, LandingSectionType.CategorySection, categoryId, serviceId, sortOrder);

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
