namespace Nestly.BuildingBlocks.Results;

/// <summary>
/// Error categories per project error-handling standards:
/// Validation, Business, Infrastructure, Unexpected — plus HTTP-mappable refinements.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Business,
    Unauthorized,
    Forbidden,
    Infrastructure,
    Unexpected
}
