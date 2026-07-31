namespace Nestly.Domain;

/// <summary>
/// The support ticket state transition matrix (SRS 31.2, task 86). Same
/// separate-table-of-transitions approach as <see cref="BookingLifecycle"/>.
/// SRS 31.2 gives "transition examples", not an exhaustive list - Open→Closed
/// (e.g. duplicate/spam) and Resolved→InProgress (customer reopens an
/// unsatisfactory resolution) are practical extensions kept close to the
/// spirit of the examples given.
/// </summary>
public static class SupportTicketLifecycle
{
    private static readonly Dictionary<SupportTicketStatus, SupportTicketStatus[]> Transitions = new()
    {
        [SupportTicketStatus.Open] = [SupportTicketStatus.InProgress, SupportTicketStatus.Escalated, SupportTicketStatus.Closed],
        [SupportTicketStatus.InProgress] = [SupportTicketStatus.WaitingForCustomer, SupportTicketStatus.Resolved, SupportTicketStatus.Escalated],
        [SupportTicketStatus.WaitingForCustomer] = [SupportTicketStatus.InProgress, SupportTicketStatus.Resolved, SupportTicketStatus.Escalated],
        [SupportTicketStatus.Escalated] = [SupportTicketStatus.InProgress, SupportTicketStatus.Resolved],
        [SupportTicketStatus.Resolved] = [SupportTicketStatus.Closed, SupportTicketStatus.InProgress],
        [SupportTicketStatus.Closed] = [],
    };

    public static bool IsValidTransition(SupportTicketStatus from, SupportTicketStatus to) =>
        Transitions[from].Contains(to);

    public static bool IsTerminal(SupportTicketStatus status) =>
        Transitions[status].Length == 0;
}
