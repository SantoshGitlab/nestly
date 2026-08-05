using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Chat;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Customer-facing chat over a booking or support-ticket thread (task 191).
/// Every action is scoped to the caller's own customer id - never a route/body
/// parameter - same convention as BookingsController/SupportTicketsController.
/// REST is the actual send/read path (works with or without a live socket);
/// <c>ChatHub</c> (mapped at <c>/hubs/chat</c>, see Program.cs) only pushes
/// live updates to a thread once a client has GETten/POSTed through here at
/// least once - see ChatHub's own doc comment for the full real-time design.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/chat/threads")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IValidator<GetOrCreateChatThreadRequest> _getOrCreateValidator;
    private readonly IValidator<SendChatMessageRequest> _sendValidator;

    public ChatController(
        IChatService chatService,
        IValidator<GetOrCreateChatThreadRequest> getOrCreateValidator,
        IValidator<SendChatMessageRequest> sendValidator)
    {
        _chatService = chatService;
        _getOrCreateValidator = getOrCreateValidator;
        _sendValidator = sendValidator;
    }

    /// <summary>Returns the thread for a booking/support-ticket context, creating it on first use (task 191).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatThreadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrCreateThread([FromBody] GetOrCreateChatThreadRequest request)
    {
        var validation = await _getOrCreateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _chatService.GetOrCreateThreadAsync(CurrentCustomerId(), request.ContextType, request.ContextId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Paginated history, oldest first (task 191, 192).</summary>
    [HttpGet("{threadId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessagePageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 50 : pageSize;

        var result = await _chatService.GetHistoryAsync(CurrentCustomerId(), threadId, page, pageSize);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Sends a message - the REST send path task 190 calls out explicitly, not just a fallback for a broken socket.</summary>
    [HttpPost("{threadId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(Guid threadId, [FromBody] SendChatMessageRequest request)
    {
        var validation = await _sendValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _chatService.SendMessageAsync(CurrentCustomerId(), threadId, request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>Marks every message not sent by this customer as read (task 192 read receipts).</summary>
    [HttpPost("{threadId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid threadId)
    {
        var result = await _chatService.MarkReadAsync(CurrentCustomerId(), threadId);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();

    private static ModelStateDictionary ToModelState(ValidationResult validation)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in validation.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return modelState;
    }
}
