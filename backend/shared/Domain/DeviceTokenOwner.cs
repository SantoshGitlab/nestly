namespace Nestly.Domain;

/// <summary>Which side of the marketplace a <see cref="DeviceToken"/> belongs to (task 277).</summary>
public enum DeviceTokenOwnerKind
{
    Customer = 0,
    Provider = 1
}

/// <summary>
/// The single owner of a <see cref="DeviceToken"/> - a customer or a provider,
/// never both and never neither (task 277).
///
/// <para>
/// <b>Why this type exists rather than two loose nullable Guids.</b> The
/// storage shape is two nullable FK columns (<c>customer_id</c>,
/// <c>provider_id</c>) so that both keep real referential integrity; see
/// <c>DeviceTokenConfiguration</c> for why that shape was chosen over a
/// polymorphic owner_type/owner_id pair. Two nullable columns are, on their
/// own, four states - and two of those four are corrupt. This type is the
/// in-process guarantee that only the two legal states are ever constructible:
/// there is no public constructor, the factories reject
/// <see cref="Guid.Empty"/>, and <see cref="FromColumns"/> is the only way back
/// from storage and throws on both-set and neither-set.
/// </para>
///
/// <para>
/// The database says the same thing with a CHECK constraint
/// (<c>ck_device_token_exactly_one_owner</c>). That constraint is the
/// authority; this type is the fail-fast copy that runs before a round trip
/// and that the test suite can actually exercise - see
/// <c>DeviceTokenConfiguration</c>'s note on which provider enforces what.
/// </para>
/// </summary>
public readonly record struct DeviceTokenOwner
{
    private DeviceTokenOwner(DeviceTokenOwnerKind kind, Guid id)
    {
        Kind = kind;
        Id = id;
    }

    public DeviceTokenOwnerKind Kind { get; }

    /// <summary>The customer id or the provider id, depending on <see cref="Kind"/>. Never <see cref="Guid.Empty"/> for an owner built through the factories.</summary>
    public Guid Id { get; }

    /// <summary>The value for the <c>customer_id</c> column - null for a provider-owned token.</summary>
    public Guid? CustomerId => Kind == DeviceTokenOwnerKind.Customer ? Id : null;

    /// <summary>The value for the <c>provider_id</c> column - null for a customer-owned token.</summary>
    public Guid? ProviderId => Kind == DeviceTokenOwnerKind.Provider ? Id : null;

    public static DeviceTokenOwner ForCustomer(Guid customerId) =>
        Create(DeviceTokenOwnerKind.Customer, customerId, nameof(customerId));

    public static DeviceTokenOwner ForProvider(Guid providerId) =>
        Create(DeviceTokenOwnerKind.Provider, providerId, nameof(providerId));

    /// <summary>
    /// Rebuilds an owner from the two storage columns, rejecting the two
    /// states the CHECK constraint forbids. Throws rather than returning a
    /// nullable so that a corrupt row is loud at the point of use instead of
    /// quietly resolving to "nobody" (which, for a revoke/list ownership test,
    /// would be indistinguishable from "not yours" and could hide the corruption
    /// for a long time).
    /// </summary>
    public static DeviceTokenOwner FromColumns(Guid? customerId, Guid? providerId) => (customerId, providerId) switch
    {
        ({ } customer, null) => ForCustomer(customer),
        (null, { } provider) => ForProvider(provider),
        (null, null) => throw new InvalidOperationException(
            "A device token must have exactly one owner, but neither customer_id nor provider_id is set."),
        _ => throw new InvalidOperationException(
            "A device token must have exactly one owner, but both customer_id and provider_id are set.")
    };

    private static DeviceTokenOwner Create(DeviceTokenOwnerKind kind, Guid id, string parameterName) =>
        id == Guid.Empty
            ? throw new ArgumentException($"A {kind} device token owner id is required.", parameterName)
            : new DeviceTokenOwner(kind, id);
}
