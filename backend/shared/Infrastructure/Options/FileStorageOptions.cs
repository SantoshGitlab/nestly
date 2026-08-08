namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "FileStorage" configuration section. Not a
/// secret, has a safe production-sensible default - same reasoning as
/// <see cref="ReferralOptions"/>/<see cref="CommissionOptions"/>, no
/// ValidateOnStart.
/// </summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Directory uploads are written to, relative to the API's content root.</summary>
    public string UploadsPath { get; set; } = "App_Data/uploads";

    /// <summary>URL path segment uploads are served back under (see <c>app.UseStaticFiles</c> in each API's Program.cs).</summary>
    public string RequestPath { get; set; } = "/uploads";
}
