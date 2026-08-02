namespace Nestly.Domain;

/// <summary>
/// What a <see cref="ChatThread"/> is scoped to (PRODUCT-ENHANCEMENTS.md "3.
/// IN-APP CHAT", task 189). Deliberately closed to these two - there is no
/// free-standing "start a conversation with anyone" surface in this product.
/// </summary>
public enum ChatContextType
{
    Booking,
    SupportTicket
}
