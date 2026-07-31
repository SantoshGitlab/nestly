using System.Text.RegularExpressions;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Shared <c>{{Variable}}</c> placeholder substitution (SRS 12.17.2), used by
/// both <see cref="NotificationTemplateRenderer"/> (live dispatch) and
/// <c>NotificationTemplateManagementService</c>'s preview endpoint (task
/// 126b) - one implementation guarantees a preview renders identically to a
/// real send. An unresolved placeholder is left in the output verbatim
/// rather than throwing, since a missing optional variable (e.g. no
/// <c>{{Reason}}</c> supplied) shouldn't block the whole notification.
/// </summary>
internal static partial class NotificationPlaceholderSubstitution
{
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();

    public static string? Substitute(string? template, IReadOnlyDictionary<string, string> variables)
    {
        if (template is null)
        {
            return null;
        }

        return PlaceholderPattern().Replace(template, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }
}
