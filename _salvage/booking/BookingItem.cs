using Nestly.BuildingBlocks.Primitives;

namespace backend.shared.Application.Domain
{
    public class BookingItem : Entity<Guid>
    {
        public Guid BookingId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public decimal Price { get; private set; }

        protected BookingItem() { }

        public BookingItem(Guid id, Guid bookingId, string description, decimal price) : base(id)
        {
            BookingId = bookingId;
            Description = description;
            Price = price;
        }

        // Add other properties and methods as needed
    }
}
