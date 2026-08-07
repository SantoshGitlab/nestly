namespace Nestly.Infrastructure.Realtime;

/// <summary>SignalR group naming for live order tracking (task 273) - one group per booking, joined by every connection allowed to watch it (the owning customer, the provider on the live assignment, an admin). Mirrors <see cref="ChatGroups"/>.</summary>
public static class TrackingGroups
{
    public static string Booking(Guid bookingId) => $"booking-tracking-{bookingId:D}";
}
