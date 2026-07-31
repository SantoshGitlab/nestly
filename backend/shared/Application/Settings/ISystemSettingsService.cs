using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Settings;

/// <summary>
/// Typed read/write access to every admin-configurable settings group (SRS
/// 12.19, tasks 131a-131h). Each group is independently readable/editable;
/// every update is change-audited via <c>IAuditLogWriter</c> (T020, SRS 21) -
/// see <c>SystemSettingsService</c> for how the two commit atomically.
/// </summary>
public interface ISystemSettingsService
{
    Task<Result<BookingSettings>> GetBookingSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<BookingSettings>> UpdateBookingSettingsAsync(BookingSettings settings, CancellationToken cancellationToken = default);

    Task<Result<SlotSettings>> GetSlotSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<SlotSettings>> UpdateSlotSettingsAsync(SlotSettings settings, CancellationToken cancellationToken = default);

    Task<Result<CancellationSettings>> GetCancellationSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<CancellationSettings>> UpdateCancellationSettingsAsync(CancellationSettings settings, CancellationToken cancellationToken = default);

    Task<Result<RescheduleSettings>> GetRescheduleSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<RescheduleSettings>> UpdateRescheduleSettingsAsync(RescheduleSettings settings, CancellationToken cancellationToken = default);

    Task<Result<TaxSettings>> GetTaxSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<TaxSettings>> UpdateTaxSettingsAsync(TaxSettings settings, CancellationToken cancellationToken = default);

    Task<Result<WalletSettings>> GetWalletSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<WalletSettings>> UpdateWalletSettingsAsync(WalletSettings settings, CancellationToken cancellationToken = default);

    Task<Result<CouponSettings>> GetCouponSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<CouponSettings>> UpdateCouponSettingsAsync(CouponSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Every settings group at once, for the admin Settings landing page (task 131h).</summary>
    Task<Result<AllSystemSettingsResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
