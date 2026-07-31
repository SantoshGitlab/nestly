using Nestly.Domain;

namespace Nestly.Application.Customers;

public interface ICustomerNoteRepository
{
    Task AddAsync(CustomerNote note);

    /// <summary>Most recent first - the 360 view (SRS 12.4.2) reads notes as a timeline, newest on top.</summary>
    Task<IReadOnlyList<CustomerNote>> ListByCustomerAsync(Guid customerId);
}
