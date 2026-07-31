using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Booking-scoped view of a customer's support tickets (SRS 11.18.2 "booking reference", task 86d). Same underlying tickets as <see cref="SupportTicketsController"/>, filtered by booking.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings/{bookingId:guid}/support-tickets")]
public class BookingSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _supportTicketService;

    public BookingSupportTicketsController(ISupportTicketService supportTicketService)
    {
        _supportTicketService = supportTicketService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupportTicketSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid bookingId)
    {
        var result = await _supportTicketService.ListByBookingAsync(CurrentCustomerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
