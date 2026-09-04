using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers the storefront-facing banner read (SRS 11.1.2/11.1.3):
/// <see cref="BannerRepository.ListLiveAsync"/> must return only published,
/// in-window banners for the requested placement, ordered for display and with
/// the media asset resolved - it is what the customer home hero renders.
/// </summary>
public sealed class BannerRepositoryTests
{
    // Each test gets its own throwaway database: ListLiveAsync returns every
    // live banner for a placement, so a shared fixture would let one test's
    // seed data leak into another's assertions.

    private static CmsMedia SeedMedia(NestlyDbContext context, string url, string? alt, CmsMediaType mediaType = CmsMediaType.Image)
    {
        var media = new CmsMedia(Guid.NewGuid(), url, alt, mediaType);
        context.Set<CmsMedia>().Add(media);
        return media;
    }

    private static Banner HomeBanner(
        Guid mediaId,
        string title,
        string? subtitle,
        int sortOrder,
        CmsContentStatus status,
        DateTime? start = null,
        DateTime? end = null) =>
        new(Guid.NewGuid(), title, subtitle, mediaId, null, CmsPlacement.Home, null, sortOrder, status, start, end);

    [Fact]
    public async Task ListLiveAsync_returns_only_published_in_window_home_banners_in_sort_order()
    {
        var now = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        using var _db = new TestDatabase();

        await using (var seed = _db.CreateContext())
        {
            var media = SeedMedia(seed, "/images/hero/a.jpg", "Alt A");
            await seed.SaveChangesAsync();

            seed.Set<Banner>().AddRange(
                // Live, but seeded second with a lower sort order - proves ordering, not insertion order.
                HomeBanner(media.Id, "Second", "sub-2", sortOrder: 1, CmsContentStatus.Published),
                HomeBanner(media.Id, "First", "sub-1", sortOrder: 0, CmsContentStatus.Published),
                // Excluded: still a draft.
                HomeBanner(media.Id, "Draft", null, sortOrder: 0, CmsContentStatus.Draft),
                // Excluded: publish window has not opened yet.
                HomeBanner(media.Id, "Future", null, sortOrder: 0, CmsContentStatus.Published, start: now.AddDays(1)),
                // Excluded: publish window already closed.
                HomeBanner(media.Id, "Expired", null, sortOrder: 0, CmsContentStatus.Published, end: now.AddDays(-1)));
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var repository = new BannerRepository(context);

        var live = await repository.ListLiveAsync(CmsPlacement.Home, now);

        live.Select(b => b.Title).Should().Equal("First", "Second");
        live[0].Subtitle.Should().Be("sub-1");
        live[0].ImageUrl.Should().Be("/images/hero/a.jpg");
        live[0].ImageAltText.Should().Be("Alt A");
        live[0].MediaType.Should().Be(CmsMediaType.Image);
    }

    [Fact]
    public async Task ListLiveAsync_carries_the_media_type_through_for_video_banners()
    {
        var now = DateTime.UtcNow;
        using var _db = new TestDatabase();

        await using (var seed = _db.CreateContext())
        {
            var media = SeedMedia(seed, "/uploads/hero.mp4", "Hero clip", CmsMediaType.Video);
            await seed.SaveChangesAsync();

            seed.Set<Banner>().Add(HomeBanner(media.Id, "Video banner", null, 0, CmsContentStatus.Published));
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var repository = new BannerRepository(context);

        var live = await repository.ListLiveAsync(CmsPlacement.Home, now);

        live.Should().ContainSingle().Which.MediaType.Should().Be(CmsMediaType.Video);
    }

    [Fact]
    public async Task ListLiveAsync_excludes_other_placements()
    {
        var now = DateTime.UtcNow;
        using var _db = new TestDatabase();

        await using (var seed = _db.CreateContext())
        {
            var media = SeedMedia(seed, "/images/hero/b.jpg", null);
            await seed.SaveChangesAsync();

            seed.Set<Banner>().AddRange(
                HomeBanner(media.Id, "Home one", null, 0, CmsContentStatus.Published),
                // A published Promotional banner must not leak into a Home query.
                new Banner(Guid.NewGuid(), "Promo", null, media.Id, null, CmsPlacement.Promotional, null, 0, CmsContentStatus.Published, null, null));
            await seed.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var repository = new BannerRepository(context);

        var live = await repository.ListLiveAsync(CmsPlacement.Home, now);

        live.Should().ContainSingle().Which.Title.Should().Be("Home one");
    }

    [Fact]
    public async Task ListLiveAsync_returns_empty_when_no_banners_qualify()
    {
        using var _db = new TestDatabase();
        await using var context = _db.CreateContext();
        var repository = new BannerRepository(context);

        var live = await repository.ListLiveAsync(CmsPlacement.Home, DateTime.UtcNow);

        live.Should().BeEmpty();
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData("  Trusted pros  ", "Trusted pros")]
    public void Subtitle_is_trimmed_and_blank_collapses_to_null(string? input, string? expected)
    {
        var banner = new Banner(
            Guid.NewGuid(), "Title", input, Guid.NewGuid(), null, CmsPlacement.Home, null, 0, CmsContentStatus.Draft, null, null);

        banner.Subtitle.Should().Be(expected);
    }
}
