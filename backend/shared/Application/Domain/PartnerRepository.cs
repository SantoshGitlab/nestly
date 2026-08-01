using Nestly.Application.PartnerManagement;
using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerRepository : IRepository<Partner>
{
    Task<bool> ExistsByPhoneAsync(string phone);
    Task<Partner?> GetByPhoneAsync(string phone);

    /// <summary>Search/filter with pagination for the admin partner list (task 150a) - mirrors <c>ICustomerRepository.SearchAsync</c>.</summary>
    Task<PartnerSearchResult> SearchAsync(PartnerSearchFilter filter);
}
