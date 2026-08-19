namespace Nestly.Domain;

/// <summary>
/// A <see cref="CustomerAmcContract"/>'s lifecycle status (docs/AMC.md).
/// <see cref="Exhausted"/> and <see cref="Expired"/> are both terminal but
/// distinct: exhaustion means the customer used every visit they paid for
/// (a success, not a failure) while the term still has time left; expiry
/// means the term ran out regardless of how many visits remained unused.
/// Reporting both separately is what lets the admin renewal report tell
/// "customers who got full value and should be offered a fresh plan" apart
/// from "customers who under-redeemed and lost unused entitlement" - two
/// different renewal conversations. <see cref="Cancelled"/> is the
/// customer-initiated terminal state, mirroring <see cref="CustomerSubscriptionStatus.Cancelled"/>.
/// </summary>
public enum CustomerAmcContractStatus
{
    Active,
    Exhausted,
    Expired,
    Cancelled
}
