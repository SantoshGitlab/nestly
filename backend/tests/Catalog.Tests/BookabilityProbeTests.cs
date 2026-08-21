using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Readiness;

namespace Nestly.Catalog.Tests;

/// <summary>
/// The startup/health bookability check (task 389, PRODUCTION-READINESS.md
/// 5.1, QA-REPORT-2026-08-18 Phase 1).
///
/// <para>
/// Every test builds the complete chain a customer walks - state, city, zone,
/// pincode, locality, category, service, serviceability mapping, slot window,
/// slot-window rule, category/city mapping - and then removes exactly one
/// link. That shape is the point: the bug being guarded against was not a
/// wrong answer but a correct, empty one, so a probe that reported "not
/// bookable" for the wrong reason would look identical to a working one on a
/// database that happens to be empty. Asserting on which gap comes back is
/// what separates the two.
/// </para>
///
/// <para>
/// A fresh <see cref="TestDatabase"/> per test rather than a class fixture:
/// these assertions are about the absence of rows, so they cannot share a
/// database with each other.
/// </para>
/// </summary>
public sealed class BookabilityProbeTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// The whole chain, with every link present. Individual tests take one
    /// link away before saving; nothing here is hard-coded to ids, so a test
    /// can also mutate a row (deactivate it, point it at another city) rather
    /// than only omit it.
    /// </summary>
    private sealed class Chain
    {
        public required State State { get; init; }
        public required City City { get; init; }
        public required Zone Zone { get; init; }
        public required Pincode Pincode { get; init; }
        public required Locality Locality { get; init; }
        public required Category Category { get; init; }
        public required Service Service { get; init; }
        public required ServicePincodeMapping ServicePincodeMapping { get; init; }
        public required SlotWindow SlotWindow { get; init; }
        public required SlotWindowRule SlotWindowRule { get; init; }
        public required CategoryCityMapping CategoryCityMapping { get; init; }
    }

    private static Chain BuildChain()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, Guid.NewGuid().ToString("N")[..6]);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Koramangala");
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

        return new Chain
        {
            State = state,
            City = city,
            Zone = zone,
            Pincode = pincode,
            Locality = locality,
            Category = category,
            Service = service,
            ServicePincodeMapping = new ServicePincodeMapping(Guid.NewGuid(), service.Id, pincode.Id),
            SlotWindow = window,
            SlotWindowRule = new SlotWindowRule(Guid.NewGuid(), window.Id, DayOfWeek.Monday),
            CategoryCityMapping = new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id),
        };
    }

    /// <summary>
    /// Saves the chain, minus whatever <paramref name="omit"/> names, after
    /// letting <paramref name="mutate"/> alter it. Foreign keys are enforced
    /// in this suite (see <see cref="TestDatabase"/>), so the insert order
    /// here is the real dependency order.
    /// </summary>
    private async Task<BookabilityReport> InspectAfterSeedingAsync(
        Action<Chain>? mutate = null,
        Func<Chain, object>? omit = null)
    {
        var chain = BuildChain();
        mutate?.Invoke(chain);
        object? omitted = omit?.Invoke(chain);

        await using (var context = _database.CreateContext())
        {
            foreach (object entity in new object[]
            {
                chain.State, chain.City, chain.Zone, chain.Pincode, chain.Locality,
                chain.Category, chain.Service, chain.ServicePincodeMapping,
                chain.SlotWindow, chain.SlotWindowRule, chain.CategoryCityMapping,
            })
            {
                if (!ReferenceEquals(entity, omitted))
                {
                    context.Add(entity);
                }
            }

            await context.SaveChangesAsync();
        }

        return await InspectAsync();
    }

    private async Task<BookabilityReport> InspectAsync()
    {
        await using var context = _database.CreateContext();
        return await new BookabilityProbe(context).InspectAsync();
    }

    [Fact]
    public async Task Reports_a_complete_chain_as_ready()
    {
        var report = await InspectAfterSeedingAsync();

        report.IsBookable.Should().BeTrue();
        report.IsDiscoverable.Should().BeTrue();
        report.Gaps.Should().BeEmpty();
        report.Describe().Should().Contain("passed");
    }

    /// <summary>
    /// The state a production database lands in from migrations alone: schema,
    /// no data. This is the case the whole task exists for.
    /// </summary>
    [Fact]
    public async Task Reports_an_empty_database_as_unbookable_and_names_every_missing_link()
    {
        var report = await InspectAsync();

        report.IsReady.Should().BeFalse();
        report.Gaps.Should().BeEquivalentTo(new[]
        {
            BookabilityGap.NoActiveCity,
            BookabilityGap.NoActiveService,
            BookabilityGap.NoSlotWindow,
        });
    }

    [Fact]
    public async Task Describes_an_unbookable_database_in_words_an_operator_cannot_read_past()
    {
        var report = await InspectAsync();

        report.Describe().Should().Contain("NOTHING CAN BE BOOKED");
        // The remedy has to travel with the verdict: the codes alone are the
        // silence this replaces, one indirection further out.
        report.Describe().Should().Contain(BookabilityGap.NoActiveCity.Code);
        report.Describe().Should().Contain(BookabilityGap.NoActiveCity.Remedy);
    }

    /// <summary>
    /// The finding itself: QA-REPORT-2026-08-18 Phase 1 found zero rows in
    /// <c>service_pincode_mapping</c> for any seeded city.
    /// </summary>
    [Fact]
    public async Task Reports_a_missing_service_pincode_mapping()
    {
        var report = await InspectAfterSeedingAsync(omit: chain => chain.ServicePincodeMapping);

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoServicePincodeMapping);
    }

    /// <summary>
    /// The other half of the finding, and the subtler one: the windows were
    /// there, so a count of <c>slot_window</c> looked healthy. Only
    /// <c>slot_window_rule</c> was empty, which
    /// <c>SlotWindowRepository.ListActiveForCityAndDayAsync</c> treats - quite
    /// correctly - as "this window is offered on no day at all".
    /// </summary>
    [Fact]
    public async Task Reports_a_slot_window_that_carries_no_day_rule_as_its_own_distinct_gap()
    {
        var report = await InspectAfterSeedingAsync(omit: chain => chain.SlotWindowRule);

        report.IsBookable.Should().BeFalse();
        // Not NoSlotWindow: the window exists, and reporting it as missing
        // would send an operator to re-create a row that is already there.
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoSlotWindowRule);
    }

    /// <summary>
    /// A deactivated mapping is exactly how an admin suspends a service in a
    /// pincode, so the probe has to read <c>is_active</c> rather than merely
    /// count rows - a suspended-everywhere catalog is as unbookable as an
    /// unmapped one.
    /// </summary>
    [Fact]
    public async Task Treats_a_deactivated_service_pincode_mapping_as_absent()
    {
        var report = await InspectAfterSeedingAsync(mutate: chain => chain.ServicePincodeMapping.Deactivate());

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoServicePincodeMapping);
    }

    [Fact]
    public async Task Treats_a_deactivated_service_as_absent()
    {
        var report = await InspectAfterSeedingAsync(mutate: chain => chain.Service.Deactivate());

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoActiveService);
    }

    [Fact]
    public async Task Treats_a_deactivated_slot_window_as_absent()
    {
        var report = await InspectAfterSeedingAsync(mutate: chain => chain.SlotWindow.Deactivate());

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoSlotWindow);
    }

    /// <summary>
    /// A pincode with no locality is unreachable even though every other row
    /// exists: a customer address links to geography by locality id, and the
    /// slot API is entered by locality id. This is precisely what the 2026-08-18
    /// sweep had to add by hand - one zone and one locality per seeded city.
    ///
    /// <para>
    /// The serviceability gap comes with it, and correctly so: the mapping row
    /// exists but points at a pincode nothing can address, which is not a
    /// mapping anyone can use. Both sentences are true and the operator needs
    /// both, so the probe reports both rather than picking one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Reports_a_pincode_that_no_locality_reaches()
    {
        var report = await InspectAfterSeedingAsync(omit: chain => chain.Locality);

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().BeEquivalentTo(new[]
        {
            BookabilityGap.NoLocality,
            BookabilityGap.NoServicePincodeMapping,
        });
    }

    /// <summary>
    /// A city with a catalog and slot windows but no pincode. The locality and
    /// the serviceability mapping are FK children of the pincode, so they are
    /// necessarily absent too - hence both gaps, not just the first.
    /// </summary>
    [Fact]
    public async Task Reports_a_city_with_no_pincode()
    {
        var chain = BuildChain();

        await using (var context = _database.CreateContext())
        {
            context.AddRange(
                chain.State, chain.City, chain.Zone, chain.Category, chain.Service,
                chain.SlotWindow, chain.SlotWindowRule, chain.CategoryCityMapping);
            await context.SaveChangesAsync();
        }

        var report = await InspectAsync();

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().BeEquivalentTo(new[]
        {
            BookabilityGap.NoActivePincode,
            BookabilityGap.NoServicePincodeMapping,
        });
    }

    /// <summary>
    /// Every ingredient present, none of them in the same city. The counts an
    /// operator would run by hand ("are there slot windows? are there
    /// mappings?") all come back non-zero, and nothing is bookable anyway -
    /// which is why this gets its own gap rather than an empty list.
    /// </summary>
    [Fact]
    public async Task Reports_ingredients_that_exist_but_do_not_line_up_in_one_city()
    {
        var chain = BuildChain();
        var otherCity = new City(Guid.NewGuid(), chain.State.Id, "Mysuru");
        var strandedWindow = new SlotWindow(
            Guid.NewGuid(), otherCity.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

        await using (var context = _database.CreateContext())
        {
            context.AddRange(
                chain.State, chain.City, otherCity, chain.Zone, chain.Pincode, chain.Locality,
                chain.Category, chain.Service, chain.ServicePincodeMapping, chain.CategoryCityMapping,
                strandedWindow, new SlotWindowRule(Guid.NewGuid(), strandedWindow.Id, DayOfWeek.Monday));
            await context.SaveChangesAsync();
        }

        var report = await InspectAsync();

        report.IsBookable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.ChainDisjoint);
    }

    /// <summary>
    /// Bookable but unreachable: the slot API would serve this service, and
    /// <c>CategoryRepository.ListServiceableInCityAsync</c> never lists its
    /// category, so no customer can navigate to it. Reported separately
    /// because the fix is a different row.
    /// </summary>
    [Fact]
    public async Task Separates_bookable_from_discoverable_when_the_category_is_not_mapped_into_the_city()
    {
        var report = await InspectAfterSeedingAsync(omit: chain => chain.CategoryCityMapping);

        report.IsBookable.Should().BeTrue();
        report.IsDiscoverable.Should().BeFalse();
        report.IsReady.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoCategoryCityMapping);
    }

    [Fact]
    public async Task Treats_a_deactivated_category_city_mapping_as_absent()
    {
        var report = await InspectAfterSeedingAsync(mutate: chain => chain.CategoryCityMapping.Deactivate());

        report.IsDiscoverable.Should().BeFalse();
        report.Gaps.Should().ContainSingle().Which.Should().Be(BookabilityGap.NoCategoryCityMapping);
    }

    [Fact]
    public async Task Treats_an_inactive_category_as_undiscoverable()
    {
        var report = await InspectAfterSeedingAsync(mutate: chain => chain.Category.Deactivate());

        report.IsBookable.Should().BeTrue();
        report.IsDiscoverable.Should().BeFalse();
    }

    /// <summary>
    /// The probe must not write anything - it runs on every health check, and
    /// a diagnostic that mutates the thing it is diagnosing is worse than no
    /// diagnostic.
    /// </summary>
    [Fact]
    public async Task Writes_nothing()
    {
        await InspectAfterSeedingAsync();

        await using var context = _database.CreateContext();
        await new BookabilityProbe(context).InspectAsync();

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Health_check_degrades_rather_than_fails_when_nothing_is_bookable()
    {
        await using var context = _database.CreateContext();
        var result = await CheckHealthAsync(context);

        // Never Unhealthy: /health/ready would go to 503 and an orchestrator
        // would hold the admin API out of rotation - the very API an operator
        // needs in order to seed the missing rows.
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("NOTHING CAN BE BOOKED");
        result.Data.Should().ContainKey(BookabilityGap.NoActiveCity.Code);
    }

    [Fact]
    public async Task Health_check_passes_once_the_chain_is_complete()
    {
        await InspectAfterSeedingAsync();

        await using var context = _database.CreateContext();
        var result = await CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().BeEmpty();
    }

    /// <summary>
    /// Runs the check through the registration shape
    /// <c>AddInfrastructure</c> uses, so the Degraded failure status under
    /// test is the configured one rather than a constant restated here.
    /// </summary>
    private static Task<HealthCheckResult> CheckHealthAsync(NestlyDbContext context)
    {
        var check = new BookabilityHealthCheck(new BookabilityProbe(context));
        var registration = new HealthCheckRegistration(
            BookabilityHealthCheck.Name,
            _ => check,
            failureStatus: HealthStatus.Degraded,
            tags: ["ready", BookabilityReadinessExtensions.BootstrapTag]);

        return check.CheckHealthAsync(new HealthCheckContext { Registration = registration });
    }
}
