using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A registered device for push delivery (SRS 19.1 push channel, task 156).
/// <see cref="Token"/> is unique platform-wide, not per owner -
/// a device/app-install can be handed from one signed-in principal to
/// another (e.g. logout/login with a different account on the same phone),
/// so registering a token already owned by someone else reassigns it here
/// rather than failing (see <c>DeviceTokenService.RegisterAsync</c>).
///
/// <para>
/// <b>Task 277 - the owner is no longer necessarily a customer.</b> Both
/// <see cref="CustomerId"/> and <see cref="ProviderId"/> are nullable and
/// exactly one is set; <see cref="DeviceTokenOwner"/> is the only way to set
/// them, and <see cref="Owner"/> the only way to read them back as a unit.
/// Before this, a provider had nowhere to register a device at all, so no
/// provider could be told a job had been assigned to them - the trigger the
/// whole tracking chain waits on.
/// </para>
///
/// <para>
/// Reassignment crosses owner kinds: re-registering a customer-owned token
/// for a provider moves it wholesale (customer_id cleared, provider_id set),
/// it does not accumulate a second owner. In practice the two apps are
/// separate installs with separate FCM/APNs tokens so this should not happen,
/// but the token column is unique platform-wide and the invariant has to hold
/// for whatever the push vendor hands us, not for what we expect.
/// </para>
/// </summary>
public class DeviceToken : Entity<Guid>
{
    /// <summary>Set iff this is a customer-owned token. Prefer <see cref="Owner"/>; these two columns exist because each is a real FK.</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Set iff this is a provider-owned token (task 277).</summary>
    public Guid? ProviderId { get; private set; }

    public DevicePlatform Platform { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>
    /// The one principal this device belongs to. Throws
    /// <see cref="InvalidOperationException"/> if the row somehow has both or
    /// neither owner column set - which the CHECK constraint makes
    /// unreachable on PostgreSQL and the constructors make unreachable
    /// in-process. Not mapped (see <c>DeviceTokenConfiguration</c>).
    /// </summary>
    public DeviceTokenOwner Owner => DeviceTokenOwner.FromColumns(CustomerId, ProviderId);

    protected DeviceToken() { }

    public DeviceToken(Guid id, DeviceTokenOwner owner, DevicePlatform platform, string token) : base(id)
    {
        SetOwner(owner);
        Platform = platform;
        Token = string.IsNullOrWhiteSpace(token)
            ? throw new ArgumentException("Device token is required.", nameof(token))
            : token;
        IsActive = true;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    /// <summary>Re-registers this token for <paramref name="owner"/> and reactivates it - covers "same owner re-registered", "handed to a different customer", and "handed across owner kinds".</summary>
    public void ReRegister(DeviceTokenOwner owner, DevicePlatform platform)
    {
        SetOwner(owner);
        Platform = platform;
        IsActive = true;
        RevokedAtUtc = null;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Exact ownership test used by the revoke path. Compares <b>both</b>
    /// columns rather than just the one the caller's kind cares about, so a
    /// hypothetical both-columns-set row matches nobody instead of matching
    /// two people - fail closed.
    /// </summary>
    public bool IsOwnedBy(DeviceTokenOwner owner) =>
        CustomerId == owner.CustomerId && ProviderId == owner.ProviderId;

    private void SetOwner(DeviceTokenOwner owner)
    {
        // DeviceTokenOwner is a struct, so default(DeviceTokenOwner) exists and
        // is (Customer, Guid.Empty) - a value its factories would never produce.
        // This is the guard that keeps that hole from reaching the database.
        if (owner.Id == Guid.Empty)
        {
            throw new ArgumentException("A device token owner id is required.", nameof(owner));
        }

        CustomerId = owner.CustomerId;
        ProviderId = owner.ProviderId;
    }
}
