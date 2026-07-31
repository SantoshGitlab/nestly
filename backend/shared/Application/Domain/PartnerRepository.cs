using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerRepository : IRepository<Partner>
{
    Task<bool> ExistsByPhoneAsync(string phone);
    Task<Partner?> GetByPhoneAsync(string phone);
}
