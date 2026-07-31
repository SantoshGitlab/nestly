using Nestly.Domain;

namespace Nestly.Application.Notifications;

public interface IDeviceTokenRepository
{
    Task AddAsync(DeviceToken token);

    Task UpdateAsync(DeviceToken token);

    Task<DeviceToken?> GetByIdAsync(Guid id);

    /// <summary>Looked up regardless of current owner - see <see cref="DeviceToken"/>'s doc comment on why a token can be reassigned.</summary>
    Task<DeviceToken?> GetByTokenAsync(string token);

    Task<IReadOnlyList<DeviceToken>> ListActiveByCustomerAsync(Guid customerId);
}
