namespace Nestly.Domain;

/// <summary>
/// The refund status transition matrix (SRS 31.3: Initiated → Processing;
/// Processing → Refunded; Processing → Failed), mirroring
/// <see cref="BookingLifecycle"/>'s table-driven approach so the legal moves
/// can be read and unit-tested as one place rather than scattered guard
/// clauses on <see cref="RefundTransaction"/>.
/// </summary>
public static class RefundTransactionLifecycle
{
    private static readonly Dictionary<RefundStatus, RefundStatus[]> Transitions = new()
    {
        [RefundStatus.Initiated] = [RefundStatus.Processing, RefundStatus.Failed],
        [RefundStatus.Processing] = [RefundStatus.Refunded, RefundStatus.Failed],
        [RefundStatus.Refunded] = [],
        [RefundStatus.Failed] = [RefundStatus.Processing],
    };

    public static bool IsValidTransition(RefundStatus from, RefundStatus to) =>
        Transitions[from].Contains(to);
}
