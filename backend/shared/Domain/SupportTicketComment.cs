using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Who wrote a <see cref="SupportTicketComment"/> - distinguishes the customer's own replies from support/admin responses in the thread (SRS 12.14.2 "add response/note").</summary>
public enum SupportTicketCommentAuthorType
{
    Customer,
    Support,
    System
}

/// <summary>A single reply in a support ticket's thread (SRS 12.14.2, task 84c). Owned by its parent <see cref="SupportTicket"/> - see that aggregate's <c>AddComment</c>.</summary>
public class SupportTicketComment : Entity<Guid>
{
    public Guid SupportTicketId { get; private set; }
    public SupportTicketCommentAuthorType AuthorType { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    protected SupportTicketComment() { }

    public SupportTicketComment(Guid id, Guid supportTicketId, SupportTicketCommentAuthorType authorType, string comment) : base(id)
    {
        SupportTicketId = supportTicketId;
        AuthorType = authorType;
        Comment = string.IsNullOrWhiteSpace(comment)
            ? throw new ArgumentException("Comment text is required.", nameof(comment))
            : comment;
        CreatedAt = DateTime.UtcNow;
    }
}
