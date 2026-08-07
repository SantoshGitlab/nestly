namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Which kind of principal a hosting process authenticates (task 273). A hub
/// type is shared by all three APIs - see <see cref="BookingTrackingHub"/> for
/// why that is required rather than merely convenient - but the customer and
/// provider access tokens are claim-for-claim identical (<c>sub</c>,
/// <c>mobile</c>, <c>jti</c>; see <c>TokenService</c> and
/// <c>ProviderTokenService</c>), so a shared hub cannot tell a customer's
/// <c>sub</c> from a provider's by inspecting claims. What distinguishes them
/// is the authentication scheme that validated the token, which is fixed per
/// process: consumer-api only ever registers the customer scheme,
/// provider-api the provider one, admin-api the admin one.
/// </summary>
public enum RealtimeActorKind
{
    /// <summary>Default (zero) on purpose: an unconfigured process authorizes nobody rather than silently authorizing everybody.</summary>
    Unknown = 0,

    Customer,
    Provider,
    Admin
}

/// <summary>
/// The <see cref="RealtimeActorKind"/> of the process this code is running in.
/// Registered by whichever of <c>AddJwtAuthentication</c> /
/// <c>AddProviderJwtAuthentication</c> / <c>AddAdminJwtAuthentication</c> the
/// API called, so it cannot drift from the scheme actually in use and cannot
/// be forgotten when a new API is added. Deliberately NOT registered with a
/// fallback in <c>AddInfrastructure</c>: a process that registers no
/// authentication at all should fail to serve a hub connection, not fall back
/// to some default identity.
/// </summary>
public sealed record RealtimeActorContext(RealtimeActorKind Kind);
