namespace Nestly.Domain;

/// <summary>
/// How a disputed support ticket (SRS 11.18.1 "wrong charge / pricing
/// dispute") was resolved (task 155).
/// </summary>
public enum DisputeResolutionOutcome
{
    /// <summary>The dispute was valid - a refund is owed.</summary>
    RefundValid,

    /// <summary>The dispute was invalid - the ticket is closed/reworked with no refund.</summary>
    ClosedInvalid
}
