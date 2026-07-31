using Microsoft.EntityFrameworkCore;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence;

public sealed class NestlyDbContext : DbContext
{
    public DbSet<State> States { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Pincode> Pincodes { get; set; }
    public DbSet<Locality> Localities { get; set; }
    public DbSet<CategoryCityMapping> CategoryCityMappings { get; set; }
    public DbSet<ServicePincodeMapping> ServicePincodeMappings { get; set; }
    public DbSet<SlotWindow> SlotWindows { get; set; }
    public DbSet<SlotWindowRule> SlotWindowRules { get; set; }
    public DbSet<SlotBlackout> SlotBlackouts { get; set; }
    public DbSet<SlotBookingPolicy> SlotBookingPolicies { get; set; }
    public DbSet<ServiceCityPrice> ServiceCityPrices { get; set; }
    public DbSet<CityPricingPolicy> CityPricingPolicies { get; set; }
    public DbSet<PromotionalPrice> PromotionalPrices { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingItem> BookingItems { get; set; }
    public DbSet<BookingAddOnItem> BookingAddOnItems { get; set; }
    public DbSet<BookingStatusHistory> BookingStatusHistories { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<PaymentAttempt> PaymentAttempts { get; set; }
    public DbSet<RefundTransaction> RefundTransactions { get; set; }
    public DbSet<WalletLedgerEntry> WalletLedgerEntries { get; set; }
    public DbSet<PlatformEscrowLedger> PlatformEscrowLedgers { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<CouponRedemption> CouponRedemptions { get; set; }
    public DbSet<BookingCancellation> BookingCancellations { get; set; }
    public DbSet<BookingReschedule> BookingReschedules { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<SupportTicketComment> SupportTicketComments { get; set; }
    public DbSet<NotificationEvent> NotificationEvents { get; set; }
    public DbSet<DeviceToken> DeviceTokens { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    public NestlyDbContext(DbContextOptions<NestlyDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestlyDbContext).Assembly);
    }
}
