using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        IValidationContext context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => ((IValidator)v).ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        Error[] errors = failures
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .Distinct()
            .ToArray();

        return CreateValidationResult<TResponse>(errors);
    }

    private static TResult CreateValidationResult<TResult>(Error[] errors)
        where TResult : Result
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (Nestly.BuildingBlocks.Results.ValidationResult.WithErrors(errors) as TResult)!;
        }

        object failure = typeof(Result)
            .GetMethods()
            .First(m => m is { Name: nameof(Result.Failure), IsGenericMethod: true })
            .MakeGenericMethod(typeof(TResult).GenericTypeArguments[0])
            .Invoke(null, [Error.Validation(Nestly.BuildingBlocks.Results.ValidationResult.ErrorCode, FormatMessage(errors))])!;

        return (TResult)failure;
    }

    private static string FormatMessage(Error[] errors) =>
        string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}"));
}
