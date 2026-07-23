using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using backend.shared.Infrastructure.Persistence.Configurations;
using backend.shared.Domain;

namespace backend.shared.Domain
{
    public class SupportTicketComment : Entity<Guid>
    {
        private readonly List<NotificationEvent> _notifications = new();

        public Guid SupportTicketId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual SupportTicket SupportTicket { get; set; }
        public virtual ICollection<NotificationEvent> Notifications => _notifications;

        private void SetSupportTicketId(Guid supportTicketId)
        {
            if (SupportTicketId == default || SupportTicketId != supportTicketId)
            {
                SupportTicketId = supportTicketId;
                SupportTicket = null;
            }
        }
    }
}
