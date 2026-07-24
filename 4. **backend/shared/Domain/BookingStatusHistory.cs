using Nestly.BuildingBlocks.Primitives;

namespace backend.shared.Application.Domain
{
    public class BookingStatusHistory : Entity<Guid>
    {
        public Guid BookingId { get; private set; }
        public string Status { get; private set; } = "Pending";
        public DateTime ChangedAt { get; private set; }

        protected BookingStatusHistory() { }

        public BookingStatusHistory(Guid id, Guid bookingId, string status) : base(id)
        {
            BookingId = bookingId;
            Status = status;
            ChangedAt = DateTime.UtcNow;
        }

        // Add other properties and methods as needed
    }
}
