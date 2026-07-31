using Nestly.Domain;

namespace Nestly.Application.Notifications;

public interface INotificationEventRepository
{
    Task AddAsync(NotificationEvent notification);

    Task UpdateAsync(NotificationEvent notification);

    Task<IReadOnlyList<NotificationEvent>> ListByCustomerAsync(Guid customerId);
}
