namespace Nestly.Infrastructure.Realtime;

/// <summary>SignalR group naming for chat threads (task 190) - one group per thread, joined by every connection with access to it.</summary>
public static class ChatGroups
{
    public static string Thread(Guid threadId) => $"chat-thread-{threadId:D}";
}
