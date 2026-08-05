using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.BuildingBlocks.Middleware;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 ProblemDetails responses.
/// Never exposes stack traces or internal details to callers.
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to report.
        }
        catch (MissingSubjectClaimException exception)
        {
            // Task 259: an authenticated request whose token carries no
            // usable subject. That is a bad token, not a server fault, so it
            // gets 401 - and it is logged at Warning, without the claim
            // value, since it says nothing about the health of this service.
            _logger.LogWarning(
                "Rejected {Method} {Path}: authenticated token carries no usable subject claim",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "https://tools.ietf.org/html/rfc7235#section-3.1",
                "Unauthorized.",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "An unexpected error occurred.",
                "The request could not be processed. Please try again or contact support.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string type, string title, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["correlationId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
    }
}
