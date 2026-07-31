using FluentAssertions;
using Nestly.Application.Auditing;
using Nestly.Domain;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// The audit-log-viewer's read side (task 130, SRS 21) - exercised against a
/// real database like <see cref="PermissionAuthorizationHandlerTests"/> and
/// <see cref="AdminLoginServiceTests"/>, the two writers whose rows this
/// query service reads back. Rows are seeded directly through
/// <see cref="AuditLog"/>'s own constructor rather than through those
/// writers: what is under test here is the filtering/pagination logic, not
/// that writing works (already covered elsewhere).
/// </summary>
public class AuditLogQueryServiceTests : IDisposable
{
    private static readonly Guid AdminOneId = Guid.NewGuid();
    private static readonly Guid AdminTwoId = Guid.NewGuid();
    private static readonly DateTime BaseTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync(params AuditLog[] entries)
    {
        await using var context = _database.CreateContext();
        await context.Set<AuditLog>().AddRangeAsync(entries);
        await context.SaveChangesAsync();
    }

    private AuditLogQueryService CreateService() => new(_database.CreateContext());

    private static AuditLog Entry(
        AuditActorType actorType,
        Guid? actorId,
        string entityName,
        string entityId,
        string action,
        DateTime occurredOnUtc) =>
        new(Guid.NewGuid(), actorType, actorId, entityName, entityId, action, occurredOnUtc: occurredOnUtc);

    [Fact]
    public async Task Returns_every_row_newest_first_when_no_filter_is_set()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginFailed", BaseTimeUtc.AddMinutes(5)));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Action.Should().Be("AdminLoginFailed");
        result.Value.Items[1].Action.Should().Be("AdminLoginSucceeded");
    }

    [Fact]
    public async Task Filters_by_actor_id()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminTwoId, "AdminUser", AdminTwoId.ToString(), "AdminLoginSucceeded", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(ActorId: AdminOneId));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().ActorId.Should().Be(AdminOneId);
    }

    [Fact]
    public async Task Filters_by_actor_type()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.Anonymous, null, "AdminUser", "unknown@example.com", "AdminLoginFailed", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(ActorType: AuditActorType.Anonymous));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().ActorType.Should().Be(AuditActorType.Anonymous);
    }

    [Fact]
    public async Task Filters_by_entity_name_as_the_module_proxy()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminPermissionCheck", AdminOneId.ToString(), "PermissionGranted:catalog.write", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(EntityName: "AdminPermissionCheck"));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().EntityName.Should().Be("AdminPermissionCheck");
    }

    [Fact]
    public async Task Filters_by_action_substring()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminAccountUnlocked", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(Action: "Login"));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().Action.Should().Be("AdminLoginSucceeded");
    }

    [Fact]
    public async Task Filters_by_date_range_inclusively()
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc.AddDays(1)),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc.AddDays(2)));

        var result = await CreateService().SearchAsync(
            new AuditLogFilterRequest(FromUtc: BaseTimeUtc, ToUtc: BaseTimeUtc.AddDays(1)));

        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().OnlyContain(item => item.OccurredOnUtc <= BaseTimeUtc.AddDays(1));
    }

    [Fact]
    public async Task An_inverted_date_range_fails_validation()
    {
        var result = await CreateService().SearchAsync(
            new AuditLogFilterRequest(FromUtc: BaseTimeUtc.AddDays(1), ToUtc: BaseTimeUtc));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditLog.InvalidDateRange");
    }

    [Theory]
    [InlineData(AuditOutcome.Grant, "PermissionGranted:catalog.write")]
    [InlineData(AuditOutcome.Deny, "PermissionDenied:catalog.write")]
    [InlineData(AuditOutcome.Failure, "AdminLoginFailed")]
    [InlineData(AuditOutcome.Success, "AdminLoginSucceeded")]
    public async Task Filters_by_outcome(AuditOutcome outcome, string matchingAction)
    {
        await SeedAsync(
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminPermissionCheck", AdminOneId.ToString(), "PermissionGranted:catalog.write", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminPermissionCheck", AdminOneId.ToString(), "PermissionDenied:catalog.write", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginFailed", BaseTimeUtc),
            Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(Outcome: outcome));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().Action.Should().Be(matchingAction);
        result.Value.Items.Single().Outcome.Should().Be(outcome);
    }

    [Fact]
    public async Task Paginates_and_reports_the_total_match_count()
    {
        var entries = Enumerable.Range(0, 5)
            .Select(i => Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(),
                "AdminLoginSucceeded", BaseTimeUtc.AddMinutes(i)))
            .ToArray();
        await SeedAsync(entries);

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(Page: 2, PageSize: 2));

        result.Value.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        // Newest-first ordering: page 1 holds minutes 4,3; page 2 holds 2,1.
        result.Value.Items[0].OccurredOnUtc.Should().Be(BaseTimeUtc.AddMinutes(2));
        result.Value.Items[1].OccurredOnUtc.Should().Be(BaseTimeUtc.AddMinutes(1));
    }

    [Fact]
    public async Task An_oversized_page_size_is_clamped_rather_than_rejected()
    {
        await SeedAsync(Entry(AuditActorType.AdminUser, AdminOneId, "AdminUser", AdminOneId.ToString(), "AdminLoginSucceeded", BaseTimeUtc));

        var result = await CreateService().SearchAsync(new AuditLogFilterRequest(PageSize: 1000));

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
    }
}
