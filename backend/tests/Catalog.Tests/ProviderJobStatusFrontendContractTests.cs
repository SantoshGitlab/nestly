using System.Text.RegularExpressions;
using FluentAssertions;
using Nestly.Application.ProviderJobs;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 315 (docs/QA-REPORT-2026-08-07.md section 8): provider-api registers
/// no JsonStringEnumConverter, so <see cref="ProviderJobStatus"/> crosses the
/// wire as its ordinal (a plain number) and
/// frontend/provider-web/src/lib/jobs-types.ts's <c>JobStatus</c> enum
/// mirrors that ordinal order by hand, with no compiler or test tying the
/// two together - a backend reorder (or an insert instead of an append)
/// would silently relabel every job status badge in provider-web, and
/// nothing would fail.
///
/// This closes that gap by reading the actual TypeScript source and
/// asserting its member names and explicit ordinals match
/// <see cref="ProviderJobStatus"/> exactly, in declaration order. It is
/// deliberately a text-parse of the real file rather than a second hardcoded
/// C# list - a hardcoded list would only catch a change on the C# side,
/// wheras this catches drift introduced from either side.
///
/// FALSIFIABILITY: renaming, reordering, or changing an explicit value in
/// either enum without updating the other fails this test. Removing a
/// member from JobStatus without removing it from ProviderJobStatus (or vice
/// versa) also fails it, since the two are compared as full ordered lists,
/// not as sets.
/// </summary>
public class ProviderJobStatusFrontendContractTests
{
    [Fact]
    public void JobStatus_TypeScript_enum_matches_ProviderJobStatus_names_and_ordinals_exactly()
    {
        string[] backendNames = Enum.GetNames<ProviderJobStatus>();
        int[] backendValues = Enum.GetValues<ProviderJobStatus>().Select(v => (int)v).ToArray();

        (string Name, int Value)[] frontendMembers = ReadFrontendJobStatusEnum();

        frontendMembers.Select(m => m.Name).Should().Equal(backendNames,
            "provider-web/src/lib/jobs-types.ts's JobStatus enum must declare the same members, " +
            "in the same order, as Nestly.Application.ProviderJobs.ProviderJobStatus - it is hand-mirrored, " +
            "not generated, and the two crossing the wire as ordinals means any mismatch silently relabels " +
            "every job status badge rather than throwing.");

        frontendMembers.Select(m => m.Value).Should().Equal(backendValues,
            "each JobStatus member's explicit ordinal must match ProviderJobStatus's implicit ordinal " +
            "(declaration position) exactly.");
    }

    private static (string Name, int Value)[] ReadFrontendJobStatusEnum()
    {
        string repoRoot = FindRepoRoot();
        string jobsTypesPath = Path.Combine(repoRoot, "frontend", "provider-web", "src", "lib", "jobs-types.ts");
        File.Exists(jobsTypesPath).Should().BeTrue(
            $"the frontend file this contract test pins against must exist at {jobsTypesPath}");

        string source = File.ReadAllText(jobsTypesPath);

        // Isolates "export enum JobStatus { ... }" specifically - there is a
        // second enum (RecurrenceFrequency) later in the same file that this
        // must not accidentally match.
        Match enumBlock = Regex.Match(source, @"export enum JobStatus\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        enumBlock.Success.Should().BeTrue("jobs-types.ts must declare 'export enum JobStatus { ... }'");

        var members = new List<(string Name, int Value)>();
        foreach (Match member in Regex.Matches(enumBlock.Groups["body"].Value, @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>\d+)"))
        {
            members.Add((member.Groups["name"].Value, int.Parse(member.Groups["value"].Value)));
        }

        members.Should().NotBeEmpty("the JobStatus enum block must contain at least one 'Name = value' member");
        return members.ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nestly.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("this test must run from within the Nestly repo (a Nestly.sln must be findable above the test binary's output directory)");
        return directory!.FullName;
    }
}
