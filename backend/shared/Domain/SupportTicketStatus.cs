namespace Nestly.Domain;

/// <summary>Support ticket lifecycle status (SRS 16.2, 31.2).</summary>
public enum SupportTicketStatus
{
    Open,
    InProgress,
    WaitingForCustomer,
    Escalated,
    Resolved,
    Closed
}
