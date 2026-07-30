using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 40b: Category aggregate + repository interface.</summary>
public sealed class CategoryRepositoryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CategoryRepositoryTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task Adding_a_category_makes_it_retrievable_by_slug()
    {
        using var context = _db.CreateContext();
        var repository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "Home Cleaning", "home-cleaning-" + Guid.NewGuid(), "desc");

        await repository.AddAsync(category);

        (await repository.ExistsBySlugAsync(category.Slug)).Should().BeTrue();
        var loaded = await repository.GetBySlugAsync(category.Slug);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Home Cleaning");
    }

    [Fact]
    public async Task GetBySlugAsync_returns_null_for_an_unknown_slug()
    {
        using var context = _db.CreateContext();
        var repository = new CategoryRepository(context);

        (await repository.GetBySlugAsync("does-not-exist")).Should().BeNull();
    }
}
