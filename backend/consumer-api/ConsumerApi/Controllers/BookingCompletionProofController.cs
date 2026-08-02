using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Completion proof (photos + checklist) for a booking (SRS 11.13, task 198). Read-only, same shape RefundsController exposes for refund status.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings/{bookingId:guid}/completion-proof")]
public class BookingCompletionProofController : ControllerBase
{
    private readonly IBookingCompletionProofRepository _completionProofRepository;
    private readonly IBookingRepository _bookingRepository;

    public BookingCompletionProofController(IBookingCompletionProofRepository completionProofRepository, IBookingRepository bookingRepository)
    {
        _completionProofRepository = completionProofRepository;
        _bookingRepository = bookingRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(BookingCompletionProofResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bookingId)
    {
        var result = await _completionProofRepository.GetForCustomerAsync(_bookingRepository, CurrentCustomerId(), bookingId);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return result.Value is null ? NoContent() : Ok(result.Value);
    }

    private Guid CurrentCustomerId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
