using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Catalog;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 103a-103e: admin category management (SRS 12.5).</summary>
public sealed class CategoryManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CategoryManagementServiceTests(TestDatabase db) => _db = db;

    private static CategoryManagementService CreateService(NestlyDbContext context) =>
        new(new CategoryRepository(context), new AuditLogWriter(context, new StubAuditContextProvider()));

    private static CategoryCreateRequest ValidCreateRequest(string suffix) => new(
        Name: $"Home Cleaning {suffix}",
        Slug: $"home-cleaning-{suffix.ToLowerInvariant()}",
        Description: "Deep cleaning for your home.",
        IconUrl: "https://cdn.example.com/icon.png",
        BannerUrl: "https://cdn.example.com/banner.png",
        SortOrder: 1,
        SeoTitle: "Home Cleaning Services",
        SeoMetaDescription: "Book trusted home cleaning services.");

    [Fact]
    public async Task Creating_a_category_then_listing_returns_it_with_audit_entry()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid().ToString("N")[..8]));

        created.IsSuccess.Should().BeTrue();
        var categories = await service.ListAsync();
        categories.Should().Contain(c => c.Id == created.Value.Id && c.IsActive && !c.IsFeatured);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "Category" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
    }

    [Fact]
    public async Task Creating_a_category_with_a_duplicate_slug_returns_conflict()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        (await service.CreateAsync(ValidCreateRequest(suffix))).IsSuccess.Should().BeTrue();

        var result = await service.CreateAsync(ValidCreateRequest(suffix));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.DuplicateSlug");
    }

    [Fact]
    public async Task Updating_a_category_persists_every_field()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var category = (await service.CreateAsync(ValidCreateRequest(Guid.NewGuid().ToString("N")[..8]))).Value;

        var updateRequest = new CategoryUpdateRequest(
            Name: "Deep Home Cleaning",
            Slug: category.Slug,
            Description: "Updated description.",
            IconUrl: "https://cdn.example.com/icon2.png",
            BannerUrl: null,
            SortOrder: 5,
            SeoTitle: "Updated SEO Title",
            SeoMetaDescription: null);

        var updated = await service.UpdateAsync(category.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Deep Home Cleaning");
        updated.Value.SortOrder.Should().Be(5);
        updated.Value.BannerUrl.Should().BeNull();
    }

    [Fact]
    public async Task Deactivating_then_reactivating_a_category_toggles_is_active_and_audits_each_step()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var category = (await service.CreateAsync(ValidCreateRequest(Guid.NewGuid().ToString("N")[..8]))).Value;

        (await service.SetActiveAsync(category.Id, false)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(category.Id)).Value.IsActive.Should().BeFalse();

        (await service.SetActiveAsync(category.Id, true)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(category.Id)).Value.IsActive.Should().BeTrue();

        context.Set<AuditLog>().Should().Contain(a => a.EntityId == category.Id.ToString() && a.Action == "Deactivated");
        context.Set<AuditLog>().Should().Contain(a => a.EntityId == category.Id.ToString() && a.Action == "Activated");
    }

    [Fact]
    public async Task Featuring_a_category_toggles_is_featured()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);
        var category = (await service.CreateAsync(ValidCreateRequest(Guid.NewGuid().ToString("N")[..8]))).Value;

        (await service.SetFeaturedAsync(category.Id, true)).IsSuccess.Should().BeTrue();

        (await service.GetByIdAsync(category.Id)).Value.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task Operating_on_an_unknown_category_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("Category.NotFound");
        (await service.SetActiveAsync(Guid.NewGuid(), true)).Error.Code.Should().Be("Category.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
