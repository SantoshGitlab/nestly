using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Notifications;

/// <summary>Device token registration for push delivery (SRS 19.1, task 156).</summary>
public interface IDeviceTokenService
{
    /// <summary>Registers (or re-registers/reassigns) a device token for the caller.</summary>
    Task<Result<DeviceTokenResponse>> RegisterAsync(Guid customerId, RegisterDeviceTokenRequest request);

    /// <summary>Deactivates a device token the caller owns (e.g. on logout).</summary>
    Task<Result> RevokeAsync(Guid customerId, Guid deviceTokenId);

    /// <summary>The caller's active devices.</summary>
    Task<Result<IReadOnlyList<DeviceTokenResponse>>> ListAsync(Guid customerId);
}
