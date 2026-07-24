using Nestly.BuildingBlocks.Primitives;

namespace backend.shared.Application.Domain
{
    public class BookingAddonItem : Entity<Guid>
    {
        public Guid BookingId { get; private set; }
        public Guid ServiceAddOnId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public decimal Price { get; private set; }

        protected BookingAddonItem() { }

        public BookingAddonItem(Guid id, Guid bookingId, Guid serviceAddOnId, string description, decimal price) : base(id)
        {
            BookingId = bookingId;
            ServiceAddOnId = serviceAddOnId;
            Description = description;
            Price = price;
        }

        // Add other properties and methods as needed
    }
}
