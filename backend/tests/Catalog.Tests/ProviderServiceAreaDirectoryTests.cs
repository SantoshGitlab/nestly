using FluentAssertions;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 371: filtering the admin provider directory by service-area
/// city, and resolving the city names shown per row. Both live in
/// <see cref="ProviderRepository"/>/<see cref="ProviderServiceAreaRepository"/>
/// rather than <see cref="ProviderManagementService"/> itself, so these are
/// repository-level tests (mirrors <c>ProviderMatchingServiceTests</c>'s use
/// of <see cref="ProviderServiceArea"/> for the same underlying data).
/// </summary>
public sealed class ProviderServiceAreaDirectoryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderServiceAreaDirectoryTests(TestDatabase db) => _db = db;

    private static Provider NewProvider() => new(
        Guid.NewGuid(), "Legal Name", "Display Name", ProviderType.Individual,
        "9" + Guid.NewGuid().ToString("N")[..9], null);

    [Fact]
    public async Task Search_filters_to_providers_with_an_active_service_area_in_the_given_city()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var bengaluru = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var mysuru = new City(Guid.NewGuid(), state.Id, "Mysuru");

        var coversBengaluru = NewProvider();
        var coversMysuru = NewProvider();
        var coversNeither = NewProvider();
        var coversBengaluruButInactive = NewProvider();

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.AddRange(bengaluru, mysuru);
            context.Providers.AddRange(coversBengaluru, coversMysuru, coversNeither, coversBengaluruButInactive);

            var inactiveArea = new ProviderServiceArea(Guid.NewGuid(), coversBengaluruButInactive.Id, bengaluru.Id);
            inactiveArea.Deactivate();

            context.Set<ProviderServiceArea>().AddRange(
                new ProviderServiceArea(Guid.NewGuid(), coversBengaluru.Id, bengaluru.Id),
                new ProviderServiceArea(Guid.NewGuid(), coversMysuru.Id, mysuru.Id),
                inactiveArea);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new ProviderRepository(readContext);
        var filter = new ProviderSearchFilter(null, null, null, null, bengaluru.Id, 1, 20);
        var result = await repository.SearchAsync(filter);

        result.Rows.Should().ContainSingle(p => p.Id == coversBengaluru.Id);
        result.Rows.Should().NotContain(p => p.Id == coversMysuru.Id);
        result.Rows.Should().NotContain(p => p.Id == coversNeither.Id);
        result.Rows.Should().NotContain(p => p.Id == coversBengaluruButInactive.Id);
    }

    [Fact]
    public async Task No_city_filter_returns_every_provider_regardless_of_service_area()
    {
        var provider = NewProvider();

        using (var context = _db.CreateContext())
        {
            context.Providers.Add(provider);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new ProviderRepository(readContext);
        var filter = new ProviderSearchFilter(null, null, null, null, null, 1, 20);
        var result = await repository.SearchAsync(filter);

        result.Rows.Should().Contain(p => p.Id == provider.Id);
    }

    [Fact]
    public async Task Resolves_active_service_area_city_names_per_provider_and_omits_inactive_ones()
    {
        var state = new State(Guid.NewGuid(), "Maharashtra", "MH" + Guid.NewGuid().ToString("N")[..6]);
        var mumbai = new City(Guid.NewGuid(), state.Id, "Mumbai");
        var pune = new City(Guid.NewGuid(), state.Id, "Pune");

        var multiCityProvider = NewProvider();
        var noAreasProvider = NewProvider();

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.AddRange(mumbai, pune);
            context.Providers.AddRange(multiCityProvider, noAreasProvider);

            var inactivePuneArea = new ProviderServiceArea(Guid.NewGuid(), multiCityProvider.Id, pune.Id, zoneId: null, pincodeId: null);
            // Two areas in the same city (one zone-scoped, one city-wide) should collapse to one name.
            var zoneScopedMumbai = new ProviderServiceArea(Guid.NewGuid(), multiCityProvider.Id, mumbai.Id);

            context.Set<ProviderServiceArea>().AddRange(
                new ProviderServiceArea(Guid.NewGuid(), multiCityProvider.Id, mumbai.Id),
                zoneScopedMumbai,
                inactivePuneArea);
            context.SaveChanges();

            inactivePuneArea.Deactivate();
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new ProviderServiceAreaRepository(readContext);
        var byProvider = await repository.ListActiveCityNamesByProviderAsync(
            [multiCityProvider.Id, noAreasProvider.Id]);

        byProvider[multiCityProvider.Id].Should().BeEquivalentTo(["Mumbai"]);
        byProvider.Should().NotContainKey(noAreasProvider.Id);
    }
}
