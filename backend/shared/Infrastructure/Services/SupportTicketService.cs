using Nestly.Application.Bookings;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Customer-facing support ticket workflow (SRS 11.18, 16, 24.8, tasks
/// 86a-d). Ticket status transitions (SupportTicketLifecycle) and dispute
/// resolution (task 155) are admin-side capabilities, not exposed here -
/// this service only covers what SRS 11.18 grants the customer: raise,
/// list, view, and reply.
/// </summary>
public class SupportTicketService : ISupportTicketService
{
    /// <summary>Categories SRS 11.18.2 treats as booking issues - a booking reference is mandatory for these (task 86d). Everything else (technical/general) is optional.</summary>
    private static readonly HashSet<SupportTicketCategory> BookingLinkedCategories =
    [
        SupportTicketCategory.BookingIssue,
        SupportTicketCategory.PaymentIssue,
        SupportTicketCategory.RefundIssue,
        SupportTicketCategory.ServiceQuality,
        SupportTicketCategory.ProfessionalConduct,
        SupportTicketCategory.PricingDispute
    ];

    private readonly ISupportTicketRepository _repository;
    private readonly IBookingRepository _bookingRepository;

    public SupportTicketService(ISupportTicketRepository repository, IBookingRepository bookingRepository)
    {
        _repository = repository;
        _bookingRepository = bookingRepository;
    }

    public async Task<Result<SupportTicketDetailResponse>> CreateAsync(Guid customerId, CreateSupportTicketRequest request)
    {
        if (BookingLinkedCategories.Contains(request.Category) && request.BookingId is null)
        {
            return Error.Validation(
                "SupportTicket.BookingRequired", $"A booking reference is required for '{request.Category}' tickets.");
        }

        if (request.BookingId is { } bookingId)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking is null || booking.CustomerId != customerId)
            {
                return Error.NotFound("SupportTicket.BookingNotFound", "The specified booking does not exist.");
            }
        }

        var ticket = new SupportTicket(
            Guid.NewGuid(), customerId, request.BookingId, request.Category, request.Subject, request.Description,
            request.Priority ?? SupportTicketPriority.Normal);

        await _repository.AddAsync(ticket);
        return Result.Success(ToDetailResponse(ticket));
    }

    public async Task<Result<IReadOnlyList<SupportTicketSummaryResponse>>> ListAsync(Guid customerId)
    {
        var tickets = await _repository.ListByCustomerAsync(customerId);
        IReadOnlyList<SupportTicketSummaryResponse> response = tickets.Select(ToSummary).ToList();
        return Result.Success(response);
    }

    public async Task<Result<IReadOnlyList<SupportTicketSummaryResponse>>> ListByBookingAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("SupportTicket.BookingNotFound", "The specified booking does not exist.");
        }

        var tickets = await _repository.ListByBookingAsync(bookingId);
        IReadOnlyList<SupportTicketSummaryResponse> response = tickets.Select(ToSummary).ToList();
        return Result.Success(response);
    }

    public async Task<Result<SupportTicketDetailResponse>> GetDetailAsync(Guid customerId, Guid ticketId)
    {
        var ticket = await _repository.GetByIdAsync(ticketId);
        if (ticket is null || ticket.CustomerId != customerId)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        return Result.Success(ToDetailResponse(ticket));
    }

    public async Task<Result<SupportTicketDetailResponse>> AddCommentAsync(Guid customerId, Guid ticketId, AddSupportTicketCommentRequest request)
    {
        var ticket = await _repository.GetByIdAsync(ticketId);
        if (ticket is null || ticket.CustomerId != customerId)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        ticket.AddComment(Guid.NewGuid(), SupportTicketCommentAuthorType.Customer, request.Comment);
        await _repository.UpdateAsync(ticket);
        return Result.Success(ToDetailResponse(ticket));
    }

    private static SupportTicketSummaryResponse ToSummary(SupportTicket ticket) => new(
        ticket.Id, ticket.Category, ticket.Priority, ticket.Subject, ticket.Status, ticket.BookingId, ticket.CreatedAtUtc, ticket.UpdatedAtUtc);

    private static SupportTicketDetailResponse ToDetailResponse(SupportTicket ticket) => new(
        ticket.Id,
        ticket.CustomerId,
        ticket.BookingId,
        ticket.Category,
        ticket.Priority,
        ticket.Subject,
        ticket.Description,
        ticket.Status,
        ticket.ResolutionSummary,
        ticket.IsDisputed,
        ticket.DisputeOutcome,
        ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new SupportTicketCommentResponse(c.Id, c.AuthorType, c.Comment, c.CreatedAt))
            .ToList(),
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc);
}
