namespace Nestly.Application.Storage;

/// <summary>
/// Binary file storage, currently backed by local disk only
/// (<c>LocalDiskFileStorageService</c>) - docs/DEVOPS.md OPEN DECISIONS
/// still lists the production CDN/media storage provider as unresolved, so
/// this exists as the swap point once that's decided, same "sandbox now,
/// swap the implementation later" shape as <c>SandboxPaymentGateway</c>/
/// <c>SandboxNotificationProvider</c>.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists <paramref name="content"/> under a server-generated name (the
    /// caller's <paramref name="fileNameHint"/> is never trusted as a path -
    /// only its extension, if any, is considered) and returns a path the
    /// content is servable back from, relative to this API's own origin
    /// (e.g. "/uploads/&lt;guid&gt;.jpg").
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileNameHint, string contentType, CancellationToken cancellationToken = default);
}
