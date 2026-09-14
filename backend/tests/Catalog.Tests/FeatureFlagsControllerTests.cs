using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Settings;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Catalog.Tests;

/// <summary>
/// The two new public, unauthenticated <c>GET /api/v1/feature-flags</c>
/// endpoints (consumer-api and provider-api, SRS 12.19 "Feature flags").
/// Each is a thin controller-level projection from the admin-only
/// <see cref="FeatureFlagSettings"/> group down to a smaller public DTO, with
/// no service-layer equivalent to unit test instead (unlike every other
/// <c>ISystemSettingsService</c> method, already covered by
/// <c>SystemSettingsServiceTests</c>) - so these exercise the controllers
/// themselves against a stub <see cref="ISystemSettingsService"/>, proving
/// the projection maps the right field to the right flag and never leaks the
/// admin shape.
/// </summary>
public sealed class FeatureFlagsControllerTests
{
    private static readonly FeatureFlagSettings AllEnabled = new(
        WalletEnabled: true,
        ReferralsEnabled: true,
        AmcSubscriptionsEnabled: true,
        ServiceRatingsEnabled: true,
        BookingHelpLinkEnabled: true,
        RatingsPageEnabled: true,
        CalendarViewEnabled: true,
        EarningsLedgerEnabled: true,
        OffersScreenEnabled: true,
        AutoManageServiceabilityEnabled: true);

    [Fact]
    public async Task ConsumerApi_Get_ProjectsCustomerFlags_AndCouponsEnabledFromCouponGroup()
    {
        var feature = AllEnabled with { WalletEnabled = false };
        var coupon = new CouponSettings(50, null, false, CouponsEnabled: false);
        var controller = new Nestly.ConsumerApi.Controllers.FeatureFlagsController(
            new StubSettingsService(feature, coupon))
        { ControllerContext = NewControllerContext() };

        var actionResult = await controller.Get();

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CustomerFeatureFlagsResponse>().Subject;
        response.WalletEnabled.Should().BeFalse();
        response.CouponsEnabled.Should().BeFalse();
        response.ReferralsEnabled.Should().BeTrue();
        response.AmcSubscriptionsEnabled.Should().BeTrue();
        response.ServiceRatingsEnabled.Should().BeTrue();
        response.BookingHelpLinkEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ConsumerApi_Get_ReturnsProblem_WhenFeatureGroupNotInitialized()
    {
        var controller = new Nestly.ConsumerApi.Controllers.FeatureFlagsController(
            new StubSettingsService(featureError: Error.NotFound("Settings.GroupNotInitialized", "missing")))
        { ControllerContext = NewControllerContext() };

        var actionResult = await controller.Get();

        actionResult.Should().NotBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ProviderApi_Get_ProjectsProviderFlags_AndNeverLeaksCustomerFlags()
    {
        var feature = AllEnabled with { OffersScreenEnabled = false, EarningsLedgerEnabled = false };
        var controller = new Nestly.ProviderApi.Controllers.FeatureFlagsController(
            new StubSettingsService(feature, coupon: null))
        { ControllerContext = NewControllerContext() };

        var actionResult = await controller.Get();

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ProviderFeatureFlagsResponse>().Subject;
        response.RatingsPageEnabled.Should().BeTrue();
        response.CalendarViewEnabled.Should().BeTrue();
        response.EarningsLedgerEnabled.Should().BeFalse();
        response.OffersScreenEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Both controllers read <c>HttpContext.RequestAborted</c>, which is null
    /// on a bare controller instance outside a real request pipeline - a
    /// default <see cref="DefaultHttpContext"/> is enough to give it a real
    /// (non-cancelled) token.
    /// </summary>
    private static ControllerContext NewControllerContext() =>
        new() { HttpContext = new DefaultHttpContext() };

    /// <summary>Returns fixed, caller-supplied settings for the one method each controller calls; every other member is unused by these two controllers.</summary>
    private sealed class StubSettingsService : ISystemSettingsService
    {
        private readonly Result<FeatureFlagSettings> _feature;
        private readonly Result<CouponSettings> _coupon;

        public StubSettingsService(FeatureFlagSettings feature, CouponSettings? coupon = null)
        {
            _feature = feature;
            _coupon = coupon ?? new CouponSettings(50, null, false, true);
        }

        public StubSettingsService(Error featureError)
        {
            _feature = featureError;
            _coupon = new CouponSettings(50, null, false, true);
        }

        public Task<Result<FeatureFlagSettings>> GetFeatureFlagSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_feature);

        public Task<Result<CouponSettings>> GetCouponSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_coupon);

        public Task<Result<FeatureFlagSettings>> UpdateFeatureFlagSettingsAsync(FeatureFlagSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingSettings>> GetBookingSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<BookingSettings>> UpdateBookingSettingsAsync(BookingSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SlotSettings>> GetSlotSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SlotSettings>> UpdateSlotSettingsAsync(SlotSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<CancellationSettings>> GetCancellationSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<CancellationSettings>> UpdateCancellationSettingsAsync(CancellationSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<RescheduleSettings>> GetRescheduleSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<RescheduleSettings>> UpdateRescheduleSettingsAsync(RescheduleSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TaxSettings>> GetTaxSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TaxSettings>> UpdateTaxSettingsAsync(TaxSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<WalletSettings>> GetWalletSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<WalletSettings>> UpdateWalletSettingsAsync(WalletSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<CouponSettings>> UpdateCouponSettingsAsync(CouponSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<AllSystemSettingsResponse>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
