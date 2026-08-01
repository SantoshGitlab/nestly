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
    SupportTicketUpdate,

    /// <summary>A referrer's shared code/link was used at registration (REFERRAL.md, task 172). Sent to the referrer only.</summary>
    ReferralRegistered,

    /// <summary>A referral reward (wallet credit or coupon) was disbursed (REFERRAL.md, task 172). Sent to both referrer and referee - dispatched once per recipient, same event type.</summary>
    ReferralRewardCredited
}
