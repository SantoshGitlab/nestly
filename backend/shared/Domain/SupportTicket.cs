using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer support ticket (SRS 11.18, 16, tasks 84b-c, 86a-d, 155). The
/// aggregate root for the support domain - owns its comment thread as a
/// single consistency boundary, the same way <see cref="Booking"/> owns its
/// items and status history.
///
/// <see cref="BookingId"/> is optional (SRS 11.18.2: "mandatory for booking
/// issues, optional for generic issues") - callers decide which categories
/// require it (see <c>SupportTicketService</c>), not this entity.
/// </summary>
public class SupportTicket : AggregateRoot<Guid>
{
    private readonly List<SupportTicketComment> _comments = [];

    public Guid CustomerId { get; private set; }
    public Guid? BookingId { get; private set; }
    public SupportTicketCategory Category { get; private set; }
    public SupportTicketPriority Priority { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public SupportTicketStatus Status { get; private set; }
    public string? ResolutionSummary { get; private set; }

    /// <summary>Set when an admin formally opens a dispute investigation on this ticket (task 155, SRS 11.18.1 "wrong charge / pricing dispute").</summary>
    public bool IsDisputed { get; private set; }
    public DisputeResolutionOutcome? DisputeOutcome { get; private set; }
    public DateTime? DisputeResolvedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<SupportTicketComment> Comments => _comments;

    protected SupportTicket() { }

    public SupportTicket(
        Guid id,
        Guid customerId,
        Guid? bookingId,
        SupportTicketCategory category,
        string subject,
        string description,
        SupportTicketPriority priority = SupportTicketPriority.Normal)
        : base(id)
    {
        CustomerId = customerId;
        BookingId = bookingId;
        Category = category;
        Subject = string.IsNullOrWhiteSpace(subject)
            ? throw new ArgumentException("Subject is required.", nameof(subject))
            : subject;
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Description is required.", nameof(description))
            : description;
        Priority = priority;
        Status = SupportTicketStatus.Open;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Appends a reply to the thread (SRS 12.14.2) without otherwise changing ticket status.</summary>
    public SupportTicketComment AddComment(Guid id, SupportTicketCommentAuthorType authorType, string comment)
    {
        var entry = new SupportTicketComment(id, Id, authorType, comment);
        _comments.Add(entry);
        UpdatedAtUtc = DateTime.UtcNow;
        return entry;
    }

    /// <summary>Moves the ticket to <paramref name="newStatus"/> if <see cref="SupportTicketLifecycle"/> allows it (SRS 31.2).</summary>
    public void ChangeStatus(SupportTicketStatus newStatus, string? resolutionSummary = null)
    {
        if (!SupportTicketLifecycle.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException($"Cannot transition a support ticket from {Status} to {newStatus}.");
        }

        Status = newStatus;
        if (resolutionSummary is not null)
        {
            ResolutionSummary = resolutionSummary;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Admin opens a formal dispute investigation on this ticket (task 155). Only meaningful while the ticket is still open.</summary>
    public void MarkDisputed()
    {
        if (SupportTicketLifecycle.IsTerminal(Status))
        {
            throw new InvalidOperationException($"Cannot open a dispute on a ticket that is already {Status}.");
        }

        IsDisputed = true;

        // Opening a dispute is the start of an active investigation - move
        // a still-Open ticket into InProgress so ResolveDispute's later
        // ChangeStatus(Resolved) has a valid path per SupportTicketLifecycle
        // (SRS 31.2 has no direct Open -> Resolved transition). A ticket
        // already InProgress/WaitingForCustomer/Escalated is left as-is.
        if (Status == SupportTicketStatus.Open)
        {
            ChangeStatus(SupportTicketStatus.InProgress);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the dispute's outcome and resolves the ticket in one step
    /// (task 155) - a resolved dispute always leaves the ticket Resolved,
    /// whichever way it was decided; only <see cref="ResolutionSummary"/>
    /// and <see cref="DisputeOutcome"/> differ.
    /// </summary>
    public void ResolveDispute(DisputeResolutionOutcome outcome, string resolutionSummary)
    {
        if (!IsDisputed)
        {
            throw new InvalidOperationException("This ticket has no open dispute to resolve.");
        }

        ChangeStatus(SupportTicketStatus.Resolved, resolutionSummary);
        DisputeOutcome = outcome;
        DisputeResolvedAtUtc = DateTime.UtcNow;
    }
}
