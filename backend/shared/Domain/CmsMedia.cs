using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A managed CMS media asset (SRS 12.16.2 "media upload support", task
/// 124e) - a URL reference plus accessibility metadata, mirroring
/// <see cref="ServiceMedia"/>'s shape. As of task 314, <c>Url</c> can come
/// from either a hand-typed reference to already-hosted content or a real
/// file upload through <c>IFileStorageService</c> (admin-api's
/// <c>CmsMediaController.Upload</c>) - the entity itself does not
/// distinguish the two, it only ever stores the resulting URL. Referenced
/// by <see cref="Banner"/> so its image is always a tracked, reusable asset
/// rather than a bare string field.
/// </summary>
public class CmsMedia : Entity<Guid>
{
    public string Url { get; private set; } = string.Empty;

    public string? AltText { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    protected CmsMedia() { }

    public CmsMedia(Guid id, string url, string? altText) : base(id)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Media URL is required.", nameof(url));
        }

        Url = url.Trim();
        AltText = altText;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string url, string? altText)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Media URL is required.", nameof(url));
        }

        Url = url.Trim();
        AltText = altText;
    }
}
