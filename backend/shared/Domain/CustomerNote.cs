using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An internal admin note attached to a customer record (SRS 12.4.2 "Internal
/// notes", 12.4.3 "Add notes/tags", task 101d). Append-only, the same shape
/// as <see cref="WalletLedgerEntry"/> and <see cref="SupportTicketComment"/> -
/// a note is a permanent part of the customer's admin-facing history, never
/// edited or deleted once written, so the repository intentionally exposes
/// no Update/Delete method.
/// </summary>
public class CustomerNote : Entity<Guid>
{
    public Guid CustomerId { get; private set; }

    /// <summary>The admin user who wrote this note - traceability only, not a foreign key any other aggregate depends on.</summary>
    public Guid AuthorAdminUserId { get; private set; }

    public string Note { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    protected CustomerNote() { }

    public CustomerNote(Guid id, Guid customerId, Guid authorAdminUserId, string note)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Note text is required.", nameof(note));
        }

        CustomerId = customerId;
        AuthorAdminUserId = authorAdminUserId;
        Note = note;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
