using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One partner login attempt, success or failure, keyed by the identifier
/// tried (mirrors <see cref="LoginAttempt"/> exactly). A separate table from
/// the customer <see cref="LoginAttempt"/> - not a shared one keyed by actor
/// type - for the same PARTNER.md SCOPE BOUNDARY reason as
/// <see cref="PartnerOtp"/>: a mobile number that is registered as both a
/// customer and a partner must not have one role's failed attempts count
/// toward the other's lockout.
/// </summary>
public class PartnerLoginAttempt : Entity<Guid>
{
    public string Identifier { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    protected PartnerLoginAttempt() { }

    public PartnerLoginAttempt(Guid id, string identifier, bool succeeded, DateTime occurredAtUtc) : base(id)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        Succeeded = succeeded;
        OccurredAtUtc = occurredAtUtc;
    }
}
