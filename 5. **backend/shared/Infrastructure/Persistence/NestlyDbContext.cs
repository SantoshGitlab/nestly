using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain;

namespace backend.shared.Infrastructure.Persistence
{
    public sealed class NestlyDbContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ServiceAddOn> ServiceAddOns { get; set; }
        public DbSet<SupportTicketComment> SupportTicketComments { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingItem> BookingItems { get; set; }
        public DbSet<BookingAddonItem> BookingAddonItems { get; set; }
        public DbSet<BookingStatusHistory> BookingStatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity types here
            modelBuilder.Entity<Booking>()
                .Property(b => b.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<BookingItem>()
                .Property(bi => bi.Description)
                .IsRequired();

            modelBuilder.Entity<BookingAddonItem>()
                .Property(bai => bai.Description)
                .IsRequired();
        }
    }
}
