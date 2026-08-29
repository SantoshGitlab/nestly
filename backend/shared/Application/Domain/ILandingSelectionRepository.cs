using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for the admin-curated home-page sections. Deliberately not an
/// <c>IRepository&lt;LandingSelection&gt;</c>: a selection is never edited in
/// place. The admin screen submits a whole section's picks at once and the
/// section is rewritten, which keeps ordering contiguous and makes the write
/// idempotent - so the only mutation this needs is a scoped replace.
/// </summary>
public interface ILandingSelectionRepository
{
    /// <summary>Every selection across all sections, ordered - one round trip for the admin config screen.</summary>
    Task<IReadOnlyList<LandingSelection>> ListAllAsync();

    /// <summary>Selections for one section, ordered by <see cref="LandingSelection.SortOrder"/>.</summary>
    Task<IReadOnlyList<LandingSelection>> ListBySectionAsync(LandingSectionType sectionType);

    /// <summary>
    /// Replaces every selection in <paramref name="sectionType"/> with
    /// <paramref name="replacements"/>, in one transaction. Used by New &amp;
    /// Trending and Most Booked, which are each a single flat ordered list.
    /// </summary>
    Task ReplaceSectionAsync(LandingSectionType sectionType, IReadOnlyList<LandingSelection> replacements);

    /// <summary>
    /// Replaces only the category-strip selections under
    /// <paramref name="categoryId"/>, leaving every other heading's picks
    /// untouched - so editing one category's strip never disturbs another's.
    /// </summary>
    Task ReplaceCategorySectionAsync(Guid categoryId, IReadOnlyList<LandingSelection> replacements);
}
