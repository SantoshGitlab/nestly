using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Chat;
using Nestly.Application.Identity;
using Nestly.Application.Support;
using Nestly.Domain;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Real-time transport for chat (task 190) - presence and thread-group
/// membership only. Sending a message always goes through the REST endpoints
/// (<c>ChatController</c> in consumer-api, <c>AdminChatController</c> in
/// admin-api): persisting a message and broadcasting it are the same
/// operation triggered from one place (<c>ChatMessageSentEvent</c> ->
/// <see cref="ChatHubBroadcastHandler"/>), rather than duplicating
/// validation/persistence in a second hub-invoked "SendMessage" RPC. This is
/// also exactly what PRODUCT-ENHANCEMENTS.md asks for - REST as the actual
/// send path (including for a client that never opens a socket at all), the
/// hub purely for live delivery and presence.
///
/// One hub TYPE, mapped at the same route (<see cref="HubRoutes.ChatPath"/>)
/// by consumer-api (customer JWT), admin-api (admin JWT) and provider-api
/// (provider JWT), each the default auth scheme in its own process -
/// <c>[Authorize]</c> with no explicit scheme resolves to whichever scheme is
/// default in the hosting process, so this single class works unmodified in
/// all three. That matters for group broadcast: SignalR's backplane
/// (<c>AddStackExchangeRedis</c>, wired in each API's Program.cs when Redis
/// is configured) fans a group message out to every server subscribed under
/// the hub's type name - had each API instead defined their own hub class, a
/// message persisted by one process could never reach a live connection held
/// by another, since they are separate processes with separate in-memory
/// connection tables. See docs/PRODUCT-ENHANCEMENTS.md's IN-APP CHAT section
/// for why this is the first real-time feature in the codebase to need
/// cross-process delivery at all (every previous notification channel -
/// push/SMS/email - is fire-and-forget, not a live socket).
///
/// JWT-over-SignalR: browsers cannot set an Authorization header on the
/// WebSocket handshake, so the client passes the access token via
/// <c>?access_token=</c> on the connection URL instead; each API's
/// JwtBearerOptions.OnMessageReceived (see DependencyInjection.AddJwtAuthentication
/// / AddAdminJwtAuthentication) reads it back off the query string for
/// requests under <see cref="HubRoutes.Prefix"/> - the standard,
/// Microsoft-documented pattern for SignalR + ASP.NET Core JWT auth.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly RealtimeActorContext _actorContext;
    private readonly IChatPresenceTracker _presenceTracker;
    private readonly IChatThreadRepository _threadRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        RealtimeActorContext actorContext,
        IChatPresenceTracker presenceTracker,
        IChatThreadRepository threadRepository,
        IBookingRepository bookingRepository,
        ISupportTicketRepository supportTicketRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        ILogger<ChatHub> logger)
    {
        _actorContext = actorContext;
        _presenceTracker = presenceTracker;
        _threadRepository = threadRepository;
        _bookingRepository = bookingRepository;
        _supportTicketRepository = supportTicketRepository;
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await _presenceTracker.MarkOnlineAsync(CurrentUserId(), Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _presenceTracker.MarkOfflineAsync(CurrentUserId(), Context.ConnectionId);
        if (exception is not null)
        {
            _logger.LogDebug(exception, "Chat connection {ConnectionId} closed with an error.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins this connection to the thread's broadcast group, after verifying
    /// access - a customer only for a thread on their own booking/ticket, a
    /// provider only for a thread on a booking they are the live assignment
    /// on (task 193's provider reply view), an admin holding "chat.read" for
    /// any thread (task 193's support console).
    /// </summary>
    public async Task JoinThread(Guid threadId)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            throw new HubException("Thread not found.");
        }

        if (!await CanAccessAsync(thread))
        {
            throw new HubException("You do not have access to this thread.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.Thread(threadId));
    }

    public async Task LeaveThread(Guid threadId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChatGroups.Thread(threadId));

    /// <summary>
    /// Dispatches on <see cref="RealtimeActorContext.Kind"/> - the same
    /// scheme-derived signal <see cref="BookingTrackingAuthorizer"/> uses -
    /// rather than a claim on the principal, because a customer JWT and a
    /// provider JWT are claim-for-claim identical (see
    /// <see cref="RealtimeActorKind"/>'s doc comment) and only the process's
    /// registered auth scheme actually distinguishes them. Fails closed:
    /// <see cref="RealtimeActorKind.Unknown"/> (a process with no
    /// authentication registered) is denied, not defaulted to allowed.
    /// </summary>
    private async Task<bool> CanAccessAsync(ChatThread thread) => _actorContext.Kind switch
    {
        // Admin-api registers one authorization policy per permission
        // code (task 96b), but that registration is process-wide and
        // this hub type is shared with the other two APIs, which never
        // register a "chat.read" policy - a class/method-level
        // [Authorize(Policy = "chat.read")] would throw for every
        // non-admin connection. Checking the claim directly (the same
        // claim PermissionAuthorizationHandler itself evaluates) gets
        // the same enforcement without that coupling.
        RealtimeActorKind.Admin => Context.User!.HasClaim(AdminClaimTypes.Permission, "chat.read"),
        RealtimeActorKind.Customer => await CanCustomerAccessAsync(thread),
        RealtimeActorKind.Provider => await CanProviderAccessAsync(thread),
        _ => false
    };

    private async Task<bool> CanCustomerAccessAsync(ChatThread thread)
    {
        Guid customerId = CurrentUserId();
        return thread.ContextType switch
        {
            ChatContextType.Booking =>
                (await _bookingRepository.GetByIdAsync(thread.ContextId))?.CustomerId == customerId,
            ChatContextType.SupportTicket =>
                (await _supportTicketRepository.GetByIdAsync(thread.ContextId))?.CustomerId == customerId,
            _ => false
        };
    }

    /// <summary>Same LIVE-assignment rule (status Assigned or Accepted) <c>ProviderChatService</c>/<c>ProviderJobService</c> enforce over REST - a provider has no support-ticket-context thread.</summary>
    private async Task<bool> CanProviderAccessAsync(ChatThread thread)
    {
        if (thread.ContextType != ChatContextType.Booking)
        {
            return false;
        }

        var assignment = await _assignmentRepository.GetActiveByBookingAsync(thread.ContextId);
        return assignment is not null && assignment.ProviderId == CurrentUserId();
    }

    private Guid CurrentUserId() => Guid.Parse(Context.User!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
