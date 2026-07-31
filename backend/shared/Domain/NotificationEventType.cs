namespace Nestly.Domain;

/// <summary>Notification trigger events (SRS 19.1, tasks 87a, 88a-g). OTP is deliberately absent - it already sends through <see cref="INotificationProvider"/> directly via <c>OtpService</c> and predates this event-log framework.</summary>
public enum NotificationEventType
{
    Welcome,
    BookingConfirmed,
    PaymentSuccess,
    PaymentFailed,
    BookingCancelled,
    BookingRescheduled,
    RefundProcessed,
    SupportTicketUpdate
}
