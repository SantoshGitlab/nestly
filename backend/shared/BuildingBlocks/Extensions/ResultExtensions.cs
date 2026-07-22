using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nestly.BuildingBlocks.Results;

namespace Nestly.BuildingBlocks.Extensions;

/// <summary>
/// Maps <see cref="Result"/> failures to RFC 7807 ProblemDetails responses
/// with consistent status codes across all APIs.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToProblemResult(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a success result to a problem response.");
        }

        int statusCode = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Business => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Infrastructure => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = result.Error.Code,
            Detail = result.Error.Message
        };

        if (result is ValidationResult validationResult)
        {
            problemDetails.Extensions["errors"] = validationResult.Errors
                .Select(e => new { e.Code, e.Message })
                .ToArray();
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
