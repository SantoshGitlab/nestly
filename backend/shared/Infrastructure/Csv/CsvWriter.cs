using System.Text;

namespace Nestly.Infrastructure.Csv;

/// <summary>
/// RFC 4180 CSV serialization, shared by every report/export in the Reports
/// module (SRS 12.18.2, tasks 128a-128d). Extracted rather than duplicated
/// per report: <c>ReviewModerationService</c> (task 122) hand-rolls the same
/// logic privately to itself, and this module needs the identical logic five
/// times over (one per report kind) plus once more for the async export
/// queue - a good point to promote it to a shared utility instead of a sixth
/// copy-paste.
/// </summary>
public static class CsvWriter
{
    /// <summary>Builds a UTF-8 CSV document: <paramref name="header"/> as the first line, then one line per row.</summary>
    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', header.Select(Field)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Field)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Characters that Excel/Sheets/LibreOffice interpret as the start of a
    /// formula when a cell begins with one of them - CSV/"formula injection"
    /// (OWASP injection family). Every report exported by this class draws at
    /// least one column from user-supplied input (e.g. review text, customer
    /// notes), so a value of e.g. <c>=HYPERLINK("http://evil","x")</c> must
    /// never reach the file verbatim.
    /// </summary>
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Quotes a CSV field only when needed (RFC 4180) - it contains a comma, quote, or newline - doubling any embedded quotes.</summary>
    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Neutralize formula injection first: a leading apostrophe forces
        // spreadsheet applications to treat the cell as text, and is itself
        // harmless in CSV (not a character RFC 4180 treats specially).
        if (Array.IndexOf(FormulaTriggers, value[0]) >= 0)
        {
            value = "'" + value;
        }

        bool needsQuoting = value.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuoting)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
