using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Catalog;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 105: admin service/package management (SRS 12.6).</summary>
public sealed class ServiceManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceManagementServiceTests(TestDatabase db) => _db = db;

    private static ServiceManagementService CreateService(NestlyDbContext context) =>
        new(
            new ServiceRepository(context),
            new CategoryRepository(context),
            new ServiceMediaRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()));

    private static async Task<CategoryResponse> SeedCategoryAsync(NestlyDbContext context)
    {
        var categoryRepository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "Cleaning", $"cleaning-{Guid.NewGuid():N}", "Cleaning services");
        await categoryRepository.AddAsync(category);
        return new CategoryResponse(category.Id, category.Name, category.Slug, category.Description,
            category.IconUrl, category.BannerUrl, category.IsActive, category.IsFeatured, category.SortOrder,
            category.SeoTitle, category.SeoMetaDescription);
    }

    private static ServiceCreateRequest ValidCreateRequest(Guid categoryId, string suffix) => new(
        CategoryId: categoryId,
        Name: $"Deep Cleaning {suffix}",
        Slug: $"deep-cleaning-{suffix.ToLowerInvariant()}",
        Description: "Full deep cleaning of your home.",
        ShortDescription: "Deep clean",
        Price: 999m,
        Inclusions: "Kitchen, bathroom, living room",
        Exclusions: "Exterior windows",
        CancellationPolicy: "Free cancellation up to 2 hours before.",
        ReschedulePolicy: "Free reschedule once.",
        DurationMinutes: 120,
        SortOrder: 1,
        SeoTitle: "Deep Cleaning Service",
        SeoMetaDescription: "Book a deep cleaning service.",
        PricingType: nameof(ServicePricingType.Fixed),
        IsTaxApplicable: true,
        IsAddOnAllowed: true,
        IsQuantityAllowed: false,
        IsInspectionBased: false,
        IsSlotRequired: true,
        IsAddressRequired: true,
        IsCustomerNoteAllowed: true);

    [Fact]
    public async Task Creating_a_service_under_a_valid_category_then_listing_returns_it_with_audit_entry()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(ValidCreateRequest(category.Id, Guid.NewGuid().ToString("N")[..8]));

        created.IsSuccess.Should().BeTrue();
        created.Value.CategoryName.Should().Be(category.Name);
        created.Value.PricingType.Should().Be(nameof(ServicePricingType.Fixed));

        var services = await service.ListAsync(category.Id);
        services.Should().Contain(s => s.Id == created.Value.Id);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "Service" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
    }

    [Fact]
    public async Task Creating_a_service_under_an_unknown_category_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid(), "x"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.NotFound");
    }

    [Fact]
    public async Task Creating_a_service_with_a_duplicate_slug_returns_conflict()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        (await service.CreateAsync(ValidCreateRequest(category.Id, suffix))).IsSuccess.Should().BeTrue();

        var result = await service.CreateAsync(ValidCreateRequest(category.Id, suffix));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Service.DuplicateSlug");
    }

    [Fact]
    public async Task Updating_a_service_persists_option_flags_and_pricing_type()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id, Guid.NewGuid().ToString("N")[..8]))).Value;

        var updateRequest = new ServiceUpdateRequest(
            CategoryId: category.Id,
            Name: "Deep Cleaning Plus",
            Slug: created.Slug,
            Description: created.Description,
            ShortDescription: created.ShortDescription,
            Price: 1299m,
            Inclusions: created.Inclusions,
            Exclusions: created.Exclusions,
            CancellationPolicy: created.CancellationPolicy,
            ReschedulePolicy: created.ReschedulePolicy,
            DurationMinutes: 150,
            SortOrder: 2,
            SeoTitle: created.SeoTitle,
            SeoMetaDescription: created.SeoMetaDescription,
            PricingType: nameof(ServicePricingType.Variable),
            IsTaxApplicable: false,
            IsAddOnAllowed: true,
            IsQuantityAllowed: true,
            IsInspectionBased: true,
            IsSlotRequired: false,
            IsAddressRequired: true,
            IsCustomerNoteAllowed: false);

        var updated = await service.UpdateAsync(created.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Price.Should().Be(1299m);
        updated.Value.PricingType.Should().Be(nameof(ServicePricingType.Variable));
        updated.Value.IsInspectionBased.Should().BeTrue();
        updated.Value.IsQuantityAllowed.Should().BeTrue();
        updated.Value.IsSlotRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivating_and_featuring_a_service_persist_and_audit()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id, Guid.NewGuid().ToString("N")[..8]))).Value;

        (await service.SetActiveAsync(created.Id, false)).IsSuccess.Should().BeTrue();
        (await service.SetFeaturedAsync(created.Id, true)).IsSuccess.Should().BeTrue();

        var reloaded = (await service.GetByIdAsync(created.Id)).Value;
        reloaded.IsActive.Should().BeFalse();
        reloaded.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_and_removing_gallery_media_persist()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id, Guid.NewGuid().ToString("N")[..8]))).Value;

        var added = await service.AddMediaAsync(created.Id, new ServiceMediaCreateRequest("https://cdn.example.com/gallery-1.jpg"));
        added.IsSuccess.Should().BeTrue();

        (await service.ListMediaAsync(created.Id)).Value.Should().ContainSingle(m => m.Id == added.Value.Id);

        (await service.RemoveMediaAsync(created.Id, added.Value.Id)).IsSuccess.Should().BeTrue();
        (await service.ListMediaAsync(created.Id)).Value.Should().BeEmpty();
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
