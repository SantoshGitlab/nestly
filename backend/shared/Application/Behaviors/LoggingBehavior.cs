using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Behaviors;

/// <summary>
/// Structured logging for every use case: name, outcome, and duration.
/// Request payloads are never logged (they may contain PII).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        _logger.LogInformation("Handling {RequestName}", requestName);

        TResponse response = await next();

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);

        if (response.IsSuccess)
        {
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                elapsed.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Handled {RequestName} with error {ErrorCode} in {ElapsedMs}ms",
                requestName,
                response.Error.Code,
                elapsed.TotalMilliseconds);
        }

        return response;
    }
}
