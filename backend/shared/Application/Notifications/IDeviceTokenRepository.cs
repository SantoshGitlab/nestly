using Nestly.Domain;

namespace Nestly.Application.Notifications;

public interface IDeviceTokenRepository
{
    Task AddAsync(DeviceToken token);

    Task UpdateAsync(DeviceToken token);

    Task<DeviceToken?> GetByIdAsync(Guid id);

    /// <summary>Looked up regardless of current owner - see <see cref="DeviceToken"/>'s doc comment on why a token can be reassigned.</summary>
    Task<DeviceToken?> GetByTokenAsync(string token);

    /// <summary>
    /// The owner's active devices. Task 277 replaced the customer-only
    /// overload with this one: the caller must state which kind of principal
    /// it is asking about, so "provider 5's tokens" can never be answered with
    /// "customer 5's tokens" because the two id spaces happen to collide.
    /// </summary>
    Task<IReadOnlyList<DeviceToken>> ListActiveByOwnerAsync(DeviceTokenOwner owner);
}
