using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Chat;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.ProviderApi.Controllers;

/// <summary>
/// Provider-facing chat over a booking thread (task 193's other reply view,
/// PRODUCT-ENHANCEMENTS.md "3. IN-APP CHAT" - ConsumerApi's <c>ChatController</c>
/// is the customer side, AdminApi's is the support console, this is the
/// provider app/portal one). Every action is scoped to the caller's own
/// provider id taken from the JWT - never a route/body parameter - same
/// convention as <see cref="JobsController"/>. REST is the actual send/read
/// path (works with or without a live socket); <c>ChatHub</c> (mapped at
/// <c>/hubs/chat</c>, see Program.cs) only pushes live updates to a thread
/// once a client has GETten/POSTed through here at least once.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.ProviderJwtBearerScheme)]
[Route("api/v{version:apiVersion}/chat/threads")]
public class ChatController : ControllerBase
{
    private readonly IProviderChatService _chatService;
    private readonly IValidator<GetOrCreateChatThreadRequest> _getOrCreateValidator;
    private readonly IValidator<SendChatMessageRequest> _sendValidator;

    public ChatController(
        IProviderChatService chatService,
        IValidator<GetOrCreateChatThreadRequest> getOrCreateValidator,
        IValidator<SendChatMessageRequest> sendValidator)
    {
        _chatService = chatService;
        _getOrCreateValidator = getOrCreateValidator;
        _sendValidator = sendValidator;
    }

    /// <summary>Returns the thread for a booking this provider is the live assignment on, creating it on first use.</summary>
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

        var result = await _chatService.GetOrCreateThreadAsync(CurrentProviderId(), request.ContextType, request.ContextId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Paginated history, oldest first.</summary>
    [HttpGet("{threadId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessagePageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 50 : pageSize;

        var result = await _chatService.GetHistoryAsync(CurrentProviderId(), threadId, page, pageSize);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Sends a message on a thread for a booking this provider is the live assignment on.</summary>
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

        var result = await _chatService.SendMessageAsync(CurrentProviderId(), threadId, request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>Marks every message not sent by this provider as read.</summary>
    [HttpPost("{threadId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid threadId)
    {
        var result = await _chatService.MarkReadAsync(CurrentProviderId(), threadId);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private Guid CurrentProviderId() =>
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
