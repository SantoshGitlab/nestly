using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotBookingPolicyRepository : IRepository<SlotBookingPolicy>
{
    Task<SlotBookingPolicy?> GetByCityAsync(Guid cityId);

    /// <summary>Every city's booking policy, ordered by city name (task 113c).</summary>
    Task<IReadOnlyList<SlotBookingPolicy>> ListAsync();
}
