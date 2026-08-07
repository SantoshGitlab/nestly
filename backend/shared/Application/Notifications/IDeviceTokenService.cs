using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Device token registration for push delivery (SRS 19.1, task 156; task 277
/// generalised it from customers to any <see cref="DeviceTokenOwner"/>).
///
/// <para>
/// <b>The owner is a parameter, never part of the request body.</b> Every
/// method takes the owner the caller has already been authenticated as -
/// <c>DeviceTokenOwner.ForCustomer(User.GetSubjectId())</c> in consumer-api,
/// <c>ForProvider(...)</c> in provider-api. There is deliberately no owner
/// field on <see cref="RegisterDeviceTokenRequest"/>, so "register a token
/// against somebody else's id" is not a request this API can express, and the
/// two APIs cannot be made to write each other's rows by sending a crafted
/// body. Ownership checks below compare the full owner (kind and id), so a
/// customer and a provider whose ids happened to collide are still strangers.
/// </para>
/// </summary>
public interface IDeviceTokenService
{
    /// <summary>Registers (or re-registers/reassigns) a device token for the caller.</summary>
    Task<Result<DeviceTokenResponse>> RegisterAsync(DeviceTokenOwner owner, RegisterDeviceTokenRequest request);

    /// <summary>
    /// Deactivates a device token the caller owns (e.g. on logout). A token
    /// that exists but belongs to somebody else returns the same NotFound as
    /// one that does not exist - the codebase's standing rule for "not yours"
    /// (see <c>BookingTrackingQueryService</c>'s comment on why 403 would turn
    /// the endpoint into an existence oracle).
    /// </summary>
    Task<Result> RevokeAsync(DeviceTokenOwner owner, Guid deviceTokenId);

    /// <summary>The caller's active devices.</summary>
    Task<Result<IReadOnlyList<DeviceTokenResponse>>> ListAsync(DeviceTokenOwner owner);
}
