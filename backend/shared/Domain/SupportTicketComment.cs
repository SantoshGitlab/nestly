using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class SupportTicketComment : Entity<Guid>
{
    public Guid SupportTicketId { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    protected SupportTicketComment() { }

    public SupportTicketComment(Guid id, Guid supportTicketId, string comment) : base(id)
    {
        SupportTicketId = supportTicketId;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }
}
