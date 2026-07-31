namespace Nestly.Domain;

/// <summary>
/// Where a piece of CMS content surfaces in the app (SRS 12.16.1's "Home
/// banners / Category banners / Promotional blocks / Footer links", 18.2
/// "banner visibility by page/placement should be configurable", task 124f).
/// Shared by <see cref="Banner"/> and <see cref="CmsPage"/> so placement is
/// one taxonomy, not duplicated per entity. <see cref="CategoryPage"/>
/// additionally carries a category id (see <see cref="Banner.CategoryId"/>) -
/// every other value is site-wide, not scoped to one category.
/// <see cref="Footer"/> covers SRS 12.16.1's "Footer links" without a
/// separate entity - a <see cref="CmsPage"/> placed in Footer surfaces as a
/// footer link.
/// </summary>
public enum CmsPlacement
{
    Home,
    CategoryPage,
    Promotional,
    Footer,
    General
}
