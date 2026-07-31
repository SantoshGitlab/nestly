using Nestly.Domain;

namespace Nestly.Application.Cms;

public interface ICmsFaqRepository
{
    Task<CmsFaq?> GetByIdAsync(Guid id);

    Task AddAsync(CmsFaq faq);

    Task UpdateAsync(CmsFaq faq);

    /// <summary>Filtered, paginated admin FAQ list (SRS 12.16.1's "manage FAQs" screen), ordered by sort order then recency.</summary>
    Task<CmsFaqSearchResult> SearchAsync(CmsFaqSearchFilter filter);
}
