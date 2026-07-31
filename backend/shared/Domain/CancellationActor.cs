namespace Nestly.Domain;

/// <summary>Who initiated a booking cancellation (SRS 11.14.2, task 80c).</summary>
public enum CancellationActor
{
    Customer,
    Admin,
    System
}
