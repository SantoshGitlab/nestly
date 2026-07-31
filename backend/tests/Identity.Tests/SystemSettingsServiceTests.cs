using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Settings;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Admin-configurable settings/feature-flag store (SRS 12.19, tasks
/// 131a-131h), exercised end to end through <see cref="SystemSettingsService"/>
/// over a real relational database - same rationale as
/// <see cref="AdminLoginServiceTests"/>: the audit row and the settings
/// update both happen in SQL, so a stubbed repository would not prove they
/// commit together.
/// </summary>
public sealed class SystemSettingsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private SystemSettingsService CreateService(NestlyDbContext context) =>
        new(
            new SystemSettingRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider(_actorId)),
            new StubAuditContextProvider(_actorId));

    private Guid? _actorId = Guid.NewGuid();

    private void Seed(string groupKey, string valueJson)
    {
        using var context = _database.CreateContext();
        context.Add(new SystemSetting(Guid.NewGuid(), groupKey, valueJson));
        context.SaveChanges();
    }

    [Fact]
    public async Task GetBookingSettingsAsync_ReturnsDeserializedValue_WhenGroupSeeded()
    {
        Seed(SystemSettingGroups.Booking, "{\"minLeadTimeHours\":2,\"maxAdvanceBookingDays\":30,\"maxActiveBookingsPerCustomer\":null,\"allowSameDayBooking\":true}");
        using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.GetBookingSettingsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new BookingSettings(2, 30, null, true));
    }

    [Fact]
    public async Task GetCancellationSettingsAsync_ReturnsNotFound_WhenGroupNeverSeeded()
    {
        using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.GetCancellationSettingsAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.GroupNotInitialized");
    }

    [Fact]
    public async Task UpdateCancellationSettingsAsync_PersistsNewValue_AndWritesAuditEntry()
    {
        Seed(SystemSettingGroups.Cancellation, "{\"freeCancellationWindowHours\":4,\"lateCancellationFeePercentage\":20,\"allowAdminOverride\":true}");
        var updated = new CancellationSettings(FreeCancellationWindowHours: 12, LateCancellationFeePercentage: 5, AllowAdminOverride: false);

        using (var context = _database.CreateContext())
        {
            var service = CreateService(context);
            var result = await service.UpdateCancellationSettingsAsync(updated);
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(updated);
        }

        // Re-read through a brand-new context: proves the change and the
        // audit row both actually committed to the database, not just to the
        // first context's change tracker.
        using (var context = _database.CreateContext())
        {
            var service = CreateService(context);
            var reread = await service.GetCancellationSettingsAsync();
            reread.Value.Should().Be(updated);

            var auditRows = context.Set<AuditLog>()
                .Where(a => a.EntityName == "SystemSetting" && a.EntityId == SystemSettingGroups.Cancellation)
                .ToList();
            auditRows.Should().ContainSingle();
            auditRows[0].Action.Should().Be("Updated");
            auditRows[0].ActorId.Should().Be(_actorId);
            auditRows[0].NewValues.Should().Contain("\"lateCancellationFeePercentage\":5");
        }
    }

    [Fact]
    public async Task UpdateBookingSettingsAsync_ReturnsNotFound_WhenGroupNeverSeeded()
    {
        using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.UpdateBookingSettingsAsync(new BookingSettings(1, 10, null, true));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.GroupNotInitialized");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryGroup_WhenAllSeeded()
    {
        Seed(SystemSettingGroups.Booking, "{\"minLeadTimeHours\":2,\"maxAdvanceBookingDays\":30,\"maxActiveBookingsPerCustomer\":null,\"allowSameDayBooking\":true}");
        Seed(SystemSettingGroups.Slot, "{\"defaultSlotDurationMinutes\":60,\"sameDayCutoffHours\":2,\"maxAdvanceBookingDays\":30,\"defaultSlotCapacity\":1,\"allowOverbooking\":false}");
        Seed(SystemSettingGroups.Cancellation, "{\"freeCancellationWindowHours\":4,\"lateCancellationFeePercentage\":20,\"allowAdminOverride\":true}");
        Seed(SystemSettingGroups.Reschedule, "{\"minHoursBeforeSlot\":2,\"maxReschedulesPerBooking\":2,\"lateFeeThresholdHours\":6,\"lateRescheduleFeePercentage\":10}");
        Seed(SystemSettingGroups.Tax, "{\"defaultTaxPercentage\":18,\"taxRegistrationNumber\":null,\"taxInclusivePricing\":false}");
        Seed(SystemSettingGroups.Wallet, "{\"maxWalletBalance\":50000,\"maxWalletUsagePercentagePerBooking\":100,\"walletCreditExpiryDays\":null,\"allowWalletTopUp\":true}");
        Seed(SystemSettingGroups.Coupon, "{\"maxDiscountPercentagePerCoupon\":50,\"maxActiveCouponsPerCustomer\":null,\"allowCouponStacking\":false,\"couponsEnabled\":true}");

        using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Booking.Should().Be(new BookingSettings(2, 30, null, true));
        result.Value.Coupon.CouponsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNotFound_WhenAnyGroupMissing()
    {
        Seed(SystemSettingGroups.Booking, "{\"minLeadTimeHours\":2,\"maxAdvanceBookingDays\":30,\"maxActiveBookingsPerCustomer\":null,\"allowSameDayBooking\":true}");
        using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.GetAllAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.GroupNotInitialized");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        private readonly Guid? _actorId;

        public StubAuditContextProvider(Guid? actorId) => _actorId = actorId;

        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, _actorId, IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    public void Dispose() => _database.Dispose();
}
