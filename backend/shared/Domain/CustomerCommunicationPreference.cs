using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's per-channel messaging consent (SRS 11.2.3 "Manage
/// communication preferences", channels per SRS 30.2).
///
/// Security/compliance boundary: these flags govern <em>transactional</em>
/// (booking and account updates) and <em>promotional</em> traffic only. OTP
/// and other security messages are deliberately not represented here — they
/// are not opt-out-able, because disabling them would lock the customer out
/// of their own account. Promotional defaults to off (explicit opt-in);
/// transactional defaults to on so a new customer still receives booking
/// updates.
/// </summary>
public class CustomerCommunicationPreference : Entity<Guid>
{
    public Guid CustomerId { get; private set; }

    public bool TransactionalSmsEnabled { get; private set; }
    public bool TransactionalEmailEnabled { get; private set; }
    public bool TransactionalWhatsAppEnabled { get; private set; }

    public bool PromotionalSmsEnabled { get; private set; }
    public bool PromotionalEmailEnabled { get; private set; }
    public bool PromotionalWhatsAppEnabled { get; private set; }

    /// <summary>SRS 30.2 lists Push as future; the flag is stored now so enabling it later needs no schema change.</summary>
    public bool PushEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected CustomerCommunicationPreference() { }

    private CustomerCommunicationPreference(Guid id, Guid customerId) : base(id)
    {
        CustomerId = customerId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The row a customer starts with when they have never expressed a
    /// preference, so callers never have to special-case a missing record.
    /// </summary>
    public static CustomerCommunicationPreference CreateDefault(Guid id, Guid customerId) =>
        new(id, customerId)
        {
            TransactionalSmsEnabled = true,
            TransactionalEmailEnabled = true,
            TransactionalWhatsAppEnabled = false,
            PromotionalSmsEnabled = false,
            PromotionalEmailEnabled = false,
            PromotionalWhatsAppEnabled = false,
            PushEnabled = false
        };

    public void Update(
        bool transactionalSms,
        bool transactionalEmail,
        bool transactionalWhatsApp,
        bool promotionalSms,
        bool promotionalEmail,
        bool promotionalWhatsApp,
        bool push)
    {
        TransactionalSmsEnabled = transactionalSms;
        TransactionalEmailEnabled = transactionalEmail;
        TransactionalWhatsAppEnabled = transactionalWhatsApp;
        PromotionalSmsEnabled = promotionalSms;
        PromotionalEmailEnabled = promotionalEmail;
        PromotionalWhatsAppEnabled = promotionalWhatsApp;
        PushEnabled = push;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Whether a promotional message may be sent on a given channel.</summary>
    public bool AllowsPromotional(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Sms => PromotionalSmsEnabled,
        NotificationChannel.Email => PromotionalEmailEnabled,
        _ => false
    };

    /// <summary>Whether a transactional message may be sent on a given channel.</summary>
    public bool AllowsTransactional(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Sms => TransactionalSmsEnabled,
        NotificationChannel.Email => TransactionalEmailEnabled,
        _ => false
    };
}
