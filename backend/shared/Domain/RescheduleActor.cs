namespace Nestly.Domain;

/// <summary>Who initiated a booking reschedule (SRS 11.15.2, task 82).</summary>
public enum RescheduleActor
{
    Customer,
    Admin,
    System
}
