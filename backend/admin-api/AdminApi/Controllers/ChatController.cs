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

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin support-console reply view (PRODUCT-ENHANCEMENTS.md IN-APP CHAT,
/// task 193) - view and reply on any booking/support-ticket thread, not
/// scoped to a single customer the way ConsumerApi's ChatController is.
///
/// Every action here (including <see cref="Reply"/>) is gated behind
/// "chat.read" alone, not the read/write split every other admin controller
/// in this codebase uses (e.g. SupportTicketsController's support.read vs
/// support.write). See AdminModules.Chat's doc comment for why: this
/// catalog still generates chat.write mechanically, but
/// PRODUCT-ENHANCEMENTS.md's RBAC ADDITIONS section is explicit that Chat
/// has exactly one tier ("View"), and no role is ever granted chat.write -
/// gating Reply behind a permission nothing holds would make the feature
/// unreachable, not safely locked down.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/chat/threads")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = ReadPolicy)]
public class ChatController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Chat + ".read";

    private readonly IAdminChatService _chatService;
    private readonly IValidator<GetOrCreateChatThreadRequest> _getOrCreateValidator;
    private readonly IValidator<SendChatMessageRequest> _sendValidator;

    public ChatController(
        IAdminChatService chatService,
        IValidator<GetOrCreateChatThreadRequest> getOrCreateValidator,
        IValidator<SendChatMessageRequest> sendValidator)
    {
        _chatService = chatService;
        _getOrCreateValidator = getOrCreateValidator;
        _sendValidator = sendValidator;
    }

    /// <summary>Returns (or opens) the thread for a booking/support-ticket context - an admin may proactively message a customer, not only reply.</summary>
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

        var result = await _chatService.GetOrCreateThreadAsync(request.ContextType, request.ContextId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("{threadId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessagePageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 50 : pageSize;

        var result = await _chatService.GetHistoryAsync(threadId, page, pageSize);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("{threadId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(Guid threadId, [FromBody] SendChatMessageRequest request)
    {
        var validation = await _sendValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _chatService.ReplyAsync(CurrentAdminUserId(), threadId, request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    [HttpPost("{threadId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid threadId)
    {
        var result = await _chatService.MarkReadAsync(threadId);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private Guid CurrentAdminUserId() =>
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
