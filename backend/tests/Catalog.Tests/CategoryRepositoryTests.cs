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

    [Fact]
    public async Task ListChildrenAsync_returns_only_active_children_of_the_given_parent()
    {
        using var context = _db.CreateContext();
        var repository = new CategoryRepository(context);

        var parent = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var activeChild = new Category(Guid.NewGuid(), "Kitchen Cleaning", "kitchen-cleaning-" + Guid.NewGuid(), "desc");
        activeChild.SetParent(parent.Id);
        var inactiveChild = new Category(Guid.NewGuid(), "Bathroom Cleaning", "bathroom-cleaning-" + Guid.NewGuid(), "desc");
        inactiveChild.SetParent(parent.Id);
        inactiveChild.Deactivate();
        var unrelated = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");

        await repository.AddAsync(parent);
        await repository.AddAsync(activeChild);
        await repository.AddAsync(inactiveChild);
        await repository.AddAsync(unrelated);

        var children = await repository.ListChildrenAsync(parent.Id);

        children.Should().ContainSingle(c => c.Id == activeChild.Id);
        children.Should().NotContain(c => c.Id == inactiveChild.Id || c.Id == unrelated.Id);
    }
}
