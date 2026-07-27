using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "ConnectionStrings" configuration section
/// for the persistence module (Options pattern, per module).
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required]
    public string Database { get; set; } = string.Empty;
}
