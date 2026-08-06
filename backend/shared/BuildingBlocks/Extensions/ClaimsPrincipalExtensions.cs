using System.Security.Claims;

namespace Nestly.BuildingBlocks.Extensions;

/// <summary>
/// Reading the authenticated subject id out of a bearer token, in one place
/// (task 259).
///
/// Every API resolved its own caller id with the same copy-pasted expression -
/// <c>Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value)</c>, 33
/// times across the consumer, provider and admin controllers. Besides the
/// duplication, both halves of that expression throw on a token this service
/// should simply reject: the null-forgiving <c>!</c> gives a
/// <see cref="NullReferenceException"/> when the claim is absent, and
/// <see cref="Guid.Parse(string)"/> an <see cref="FormatException"/> when it
/// is present but not a Guid. Both escape the action as unhandled exceptions
/// and surface as 500, when the honest answer is 401 - the caller's token is
/// not one this API can act on.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The JWT subject claim. Spelled literally rather than via
    /// <c>JwtRegisteredClaimNames.Sub</c> so BuildingBlocks does not take a
    /// dependency on the JWT package; every token service issues this claim
    /// and each API sets <c>MapInboundClaims = false</c>, so it arrives
    /// under its original name.
    /// </summary>
    private const string SubjectClaimType = "sub";

    /// <summary>
    /// The caller's subject id, or <see langword="null"/> when the token
    /// carries no usable one.
    /// </summary>
    public static Guid? TryGetSubjectId(this ClaimsPrincipal principal)
    {
        string? value = principal.FindFirst(SubjectClaimType)?.Value
            // Falls back to the mapped name so a principal built with the
            // default inbound claim mapping still resolves.
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out Guid id) ? id : null;
    }

    /// <summary>
    /// The caller's subject id, or a <see cref="MissingSubjectClaimException"/>
    /// (rendered as 401 by <c>GlobalExceptionHandlingMiddleware</c>) when the
    /// token carries no usable one. Use from an <c>[Authorize]</c>d action,
    /// where a token that got this far but has no subject is a bad token
    /// rather than a server fault.
    /// </summary>
    public static Guid GetSubjectId(this ClaimsPrincipal principal) =>
        principal.TryGetSubjectId() ?? throw new MissingSubjectClaimException();
}

/// <summary>
/// An authenticated request whose token carries no usable subject claim.
/// Mapped to 401 rather than 500 - see
/// <see cref="ClaimsPrincipalExtensions.GetSubjectId"/>.
/// </summary>
public sealed class MissingSubjectClaimException : Exception
{
    public MissingSubjectClaimException()
        : base("The access token does not carry a usable subject claim.")
    {
    }
}
