using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Serviceability;
using Nestly.Application.Settings;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers <see cref="ServiceabilityAutoDisableSweepJob"/> - the periodic,
/// provider-independent backstop for
/// <c>ServiceabilityMappingManagementService.AutoDisableUnservedMappingsAsync</c>'s
/// grace period (docs/OPEN-FIXES-FEATURES.csv "Service to pincode mapping"
/// follow-up). This job's candidate query is global - every mapping with a
/// pending auto-disable timer, no per-provider scope - so each test gets its
/// own fresh database, same rationale and pattern as
/// <see cref="BookingFulfilmentPromotionJobTests"/>.
/// </summary>
public sealed class ServiceabilityAutoDisableSweepJobTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private ServiceabilityAutoDisableSweepJob CreateJob(NestlyDbContext context) =>
        new(
            new ServicePincodeMappingRepository(context),
            TestServices.AuditLogWriter(context),
            TestServices.SystemSettings(context),
            NullLogger<ServiceabilityAutoDisableSweepJob>.Instance);

    /// <summary>An active mapping with a since-elapsed pending timer, and no active provider actually covering it.</summary>
    private (ServicePincodeMapping Mapping, Guid ServiceId, Guid PincodeId) SeedPastDuePendingMapping(NestlyDbContext context, bool pinned = false)
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Cleaning", "deep-cleaning-" + Guid.NewGuid(), "desc", 999m);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "560" + Guid.NewGuid().ToString("N")[..6]);
        var mapping = new ServicePincodeMapping(Guid.NewGuid(), service.Id, pincode.Id);
        if (pinned)
        {
            mapping.Pin();
        }

        mapping.MarkPendingAutoDisable(DateTime.UtcNow.AddMinutes(-(ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes + 5)));

        context.Add(category);
        context.Add(service);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Pincodes.Add(pincode);
        context.Add(mapping);
        context.SaveChanges();

        return (mapping, service.Id, pincode.Id);
    }

    [Fact]
    public async Task SweepAsync_disables_a_mapping_whose_grace_period_has_elapsed_and_is_still_unserved()
    {
        using var context = _db.CreateContext();
        var (mapping, _, _) = SeedPastDuePendingMapping(context);

        var disabledCount = await CreateJob(context).SweepAsync();

        disabledCount.Should().Be(1);

        using var readContext = _db.CreateContext();
        var reread = readContext.Set<ServicePincodeMapping>().Single(m => m.Id == mapping.Id);
        reread.IsActive.Should().BeFalse();
        reread.PendingAutoDisableSince.Should().BeNull();
        reread.LastAutoToggledAtUtc.Should().NotBeNull();

        var audit = readContext.Set<AuditLog>().Single(a => a.EntityId == mapping.Id.ToString());
        audit.Action.Should().Be("AutoDisabled");
        audit.ActorType.Should().Be(AuditActorType.System);
    }

    [Fact]
    public async Task SweepAsync_clears_the_pending_timer_when_coverage_has_returned()
    {
        using var context = _db.CreateContext();
        var (mapping, serviceId, pincodeId) = SeedPastDuePendingMapping(context);

        var provider = new Provider(Guid.NewGuid(), "Legal", "Covering Provider", ProviderType.Individual, "9" + Guid.NewGuid().ToString("N")[..9]);
        provider.ChangeStatus(ProviderStatus.Active);
        var category = context.Set<Service>().Single(s => s.Id == serviceId).CategoryId;
        var pincode = context.Set<Pincode>().Single(p => p.Id == pincodeId);
        context.Providers.Add(provider);
        context.ProviderSkillMappings.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, category));
        context.ProviderServiceAreas.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, pincode.CityId, pincodeId: pincode.Id));
        context.SaveChanges();

        var disabledCount = await CreateJob(context).SweepAsync();

        disabledCount.Should().Be(0);

        using var readContext = _db.CreateContext();
        var reread = readContext.Set<ServicePincodeMapping>().Single(m => m.Id == mapping.Id);
        reread.IsActive.Should().BeTrue();
        reread.PendingAutoDisableSince.Should().BeNull();
        readContext.Set<AuditLog>().Where(a => a.EntityId == mapping.Id.ToString()).Should().BeEmpty();
    }

    [Fact]
    public async Task SweepAsync_skips_a_pinned_mapping_and_clears_its_pending_timer()
    {
        using var context = _db.CreateContext();
        var (mapping, _, _) = SeedPastDuePendingMapping(context, pinned: true);

        var disabledCount = await CreateJob(context).SweepAsync();

        disabledCount.Should().Be(0);

        using var readContext = _db.CreateContext();
        var reread = readContext.Set<ServicePincodeMapping>().Single(m => m.Id == mapping.Id);
        reread.IsActive.Should().BeTrue();
        reread.PendingAutoDisableSince.Should().BeNull();
        readContext.Set<AuditLog>().Where(a => a.EntityId == mapping.Id.ToString()).Should().BeEmpty();
    }

    [Fact]
    public async Task SweepAsync_leaves_a_mapping_alone_while_its_grace_period_has_not_yet_elapsed()
    {
        Guid mappingId;
        using (var seedContext = _db.CreateContext())
        {
            var (mapping, _, _) = SeedPastDuePendingMapping(seedContext);
            mappingId = mapping.Id;
        }

        // Overwrite with a timer that is still within the grace period, via a
        // fresh context - a query through the same context that just seeded
        // the mapping would see its own already-tracked (past-due) copy
        // rather than this update.
        using (var overwriteContext = _db.CreateContext())
        {
            overwriteContext.Database.ExecuteSqlInterpolated(
                $"UPDATE service_pincode_mapping SET pending_auto_disable_since = {DateTime.UtcNow} WHERE id = {mappingId}");
        }

        using var context = _db.CreateContext();
        var disabledCount = await CreateJob(context).SweepAsync();

        disabledCount.Should().Be(0);
        using var readContext = _db.CreateContext();
        readContext.Set<ServicePincodeMapping>().Single(m => m.Id == mappingId).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SweepAsync_is_a_no_op_when_the_kill_switch_is_off()
    {
        using var context = _db.CreateContext();
        var (mapping, _, _) = SeedPastDuePendingMapping(context);
        context.Add(new SystemSetting(
            Guid.NewGuid(),
            SystemSettingGroups.Feature,
            "{\"walletEnabled\":true,\"referralsEnabled\":true,\"amcSubscriptionsEnabled\":true,\"serviceRatingsEnabled\":true,\"bookingHelpLinkEnabled\":true,\"ratingsPageEnabled\":true,\"calendarViewEnabled\":true,\"earningsLedgerEnabled\":true,\"offersScreenEnabled\":true,\"autoManageServiceabilityEnabled\":false}"));
        context.SaveChanges();

        var disabledCount = await CreateJob(context).SweepAsync();

        disabledCount.Should().Be(0);
        using var readContext = _db.CreateContext();
        var reread = readContext.Set<ServicePincodeMapping>().Single(m => m.Id == mapping.Id);
        reread.IsActive.Should().BeTrue();
        reread.PendingAutoDisableSince.Should().NotBeNull("the kill switch stops the sweep before it even looks at pending mappings");
        readContext.Set<AuditLog>().Should().BeEmpty();
    }

    [Fact]
    public async Task SweepAsync_does_nothing_when_no_mapping_is_pending()
    {
        using var context = _db.CreateContext();

        (await CreateJob(context).SweepAsync()).Should().Be(0);
    }
}
