namespace Nestly.BuildingBlocks.Results;

/// <summary>
/// A failure result that aggregates multiple field-level validation errors.
/// </summary>
public sealed class ValidationResult : Result
{
    public const string ErrorCode = "Validation.General";

    private ValidationResult(IReadOnlyCollection<Error> errors)
        : base(false, Error.Validation(ErrorCode, "One or more validation errors occurred."))
    {
        Errors = errors;
    }

    public IReadOnlyCollection<Error> Errors { get; }

    public static ValidationResult WithErrors(IReadOnlyCollection<Error> errors) => new(errors);
}
