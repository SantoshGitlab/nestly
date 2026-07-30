using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotBookingPolicyRepository : IRepository<SlotBookingPolicy>
{
    Task<SlotBookingPolicy?> GetByCityAsync(Guid cityId);
}
