namespace Nestly.Domain;

/// <summary>
/// Draft/publish workflow status shared by every CMS content type (SRS
/// 12.16.2 "draft/publish status", 18.2, tasks 124c/124d) - <see cref="CmsPage"/>,
/// <see cref="Banner"/>, and <see cref="CmsFaq"/> all carry one of these
/// rather than each inventing its own boolean flag.
/// </summary>
public enum CmsContentStatus
{
    Draft,
    Published
}
