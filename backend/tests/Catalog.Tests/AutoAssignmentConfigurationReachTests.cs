using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Nestly.Infrastructure.Options;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 363: <see cref="AutoAssignmentOptions.Enabled"/> is the matcher's
/// incident kill switch, and <c>ProviderAutoAssignmentHandler</c> is an
/// in-process notification handler - it runs wherever the transition to
/// <c>AwaitingFulfilment</c> is raised, which is all three API processes
/// (consumer-api reschedules via <c>RescheduleService</c>, provider-api
/// assignment rejections via <c>BookingProviderAssignmentService</c>,
/// admin-api the promotion job). Task 361 materialised the section in
/// admin-api only, so flipping <c>Enabled</c> there left auto-assignment
/// live in the other two.
///
/// Nothing in the compiler ties an appsettings section to the options class
/// it binds: a missing section silently falls back to the C# defaults, and a
/// misspelled key silently binds nothing. Both failure modes are invisible
/// until the incident where the switch is flipped and does not work, which is
/// the worst possible moment to discover them - hence a test that reads the
/// real appsettings files.
///
/// FALSIFIABILITY: deleting the AutoAssignment section from any of the three
/// base appsettings.json files fails this test, as does dropping
/// <c>Enabled</c> from one, misspelling any key, or adding the
/// admin-api-only <c>Promotion*</c> keys to a process that does not run the
/// promotion job (a knob that would silently do nothing - task 361's
/// reasoning, asserted here rather than left as prose).
/// </summary>
public class AutoAssignmentConfigurationReachTests
{
    /// <summary>
    /// Relative to the repo root. Base <c>appsettings.json</c> only:
    /// environment overlays are expected to override selectively, not to
    /// restate the whole section.
    /// </summary>
    private static readonly string[] ApiAppSettings =
    [
        Path.Combine("backend", "consumer-api", "ConsumerApi", "appsettings.json"),
        Path.Combine("backend", "provider-api", "ProviderApi", "appsettings.json"),
        Path.Combine("backend", "admin-api", "AdminApi", "appsettings.json"),
    ];

    /// <summary>
    /// The promotion sweep (<c>BookingFulfilmentPromotionJob</c>) is scheduled
    /// from admin-api's <c>Program.cs</c> alone, so these keys have reach in
    /// that process only.
    /// </summary>
    private static readonly string[] AdminOnlyKeys =
    [
        nameof(AutoAssignmentOptions.PromotionEnabled),
        nameof(AutoAssignmentOptions.PromotionLeadTimeHours),
        nameof(AutoAssignmentOptions.PromotionMaxSlotAgeHours),
        nameof(AutoAssignmentOptions.PromotionBatchSize),
    ];

    private const string AdminAppSettings = "admin-api";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_api_materialises_the_AutoAssignment_kill_switch(int apiIndex)
    {
        string relativePath = ApiAppSettings[apiIndex];
        Dictionary<string, JsonElement> section = ReadAutoAssignmentSection(relativePath);

        section.Should().ContainKey(nameof(AutoAssignmentOptions.Enabled),
            $"{relativePath} must materialise AutoAssignment:Enabled - ProviderAutoAssignmentHandler runs " +
            "in every API process that can raise a transition to AwaitingFulfilment, so a switch present " +
            "in only some of them cannot actually turn auto-assignment off during an incident.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_AutoAssignment_key_matches_a_real_options_property(int apiIndex)
    {
        string relativePath = ApiAppSettings[apiIndex];
        Dictionary<string, JsonElement> section = ReadAutoAssignmentSection(relativePath);

        string[] bindableNames = typeof(AutoAssignmentOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToArray();

        section.Keys.Should().BeSubsetOf(bindableNames,
            $"every key under AutoAssignment in {relativePath} must name a settable AutoAssignmentOptions " +
            "property - configuration binding ignores a key it does not recognise, so a typo here is a " +
            "setting that appears configured and is not.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Promotion_keys_appear_only_where_the_promotion_job_runs(int apiIndex)
    {
        string relativePath = ApiAppSettings[apiIndex];
        Dictionary<string, JsonElement> section = ReadAutoAssignmentSection(relativePath);
        bool isAdminApi = relativePath.Contains(AdminAppSettings, StringComparison.Ordinal);

        string[] promotionKeysPresent = section.Keys.Intersect(AdminOnlyKeys, StringComparer.Ordinal).ToArray();

        if (isAdminApi)
        {
            promotionKeysPresent.Should().BeEquivalentTo(AdminOnlyKeys,
                "admin-api is the process that schedules BookingFulfilmentPromotionJob, so it is the one " +
                "place these keys can actually be flipped in an incident.");
        }
        else
        {
            promotionKeysPresent.Should().BeEmpty(
                $"{relativePath} does not schedule BookingFulfilmentPromotionJob (BackgroundJobs:ServerEnabled " +
                "is admin-api's, and ScheduleBookingFulfilmentPromotion() is called only from admin-api's " +
                "Program.cs), so writing these keys here would hand ops a knob that silently does nothing.");
        }
    }

    private static Dictionary<string, JsonElement> ReadAutoAssignmentSection(string relativePath)
    {
        string fullPath = Path.Combine(FindRepoRoot(), relativePath);
        File.Exists(fullPath).Should().BeTrue($"the appsettings file this test pins against must exist at {fullPath}");

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(fullPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        document.RootElement.TryGetProperty(AutoAssignmentOptions.SectionName, out JsonElement section)
            .Should().BeTrue($"{relativePath} must carry an \"{AutoAssignmentOptions.SectionName}\" section");

        return section.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nestly.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull(
            "this test must run from within the Nestly repo (a Nestly.sln must be findable above the test binary's output directory)");
        return directory!.FullName;
    }
}
