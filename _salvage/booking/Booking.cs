using Nestly.BuildingBlocks.Primitives;

namespace backend.shared.Application.Domain
{
    public class Booking : Entity<Guid>
    {
        public Guid CustomerId { get; private set; }
        public Guid ServiceId { get; private set; }
        public DateTime SlotDate { get; private set; }
        public decimal TotalPrice { get; private set; }
        public string Status { get; private set; } = "Pending";

        protected Booking() { }

        public Booking(Guid id, Guid customerId, Guid serviceId, DateTime slotDate, decimal totalPrice) : base(id)
        {
            CustomerId = customerId;
            ServiceId = serviceId;
            SlotDate = slotDate;
            TotalPrice = totalPrice;
        }

        // Add other properties and methods as needed
    }
}
