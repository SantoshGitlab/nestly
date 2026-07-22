namespace Nestly.BuildingBlocks.Results;

/// <summary>
/// A machine-readable error with a stable code and a user-safe message.
/// Codes use the convention "Module.Reason" (e.g. "Customer.NotFound").
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Unexpected);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Business(string code, string message) => new(code, message, ErrorType.Business);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Infrastructure(string code, string message) => new(code, message, ErrorType.Infrastructure);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
