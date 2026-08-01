using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application.PartnerAvailability;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Partner recurring availability windows and blackout dates (task 149b,
/// PARTNER.md API surface "Availability").
/// </summary>
public class PartnerAvailabilityServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private Guid _partnerId;

    public PartnerAvailabilityServiceTests()
    {
        using var context = _database.CreateContext();
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");
        _partnerId = partner.Id;
        context.Add(partner);
        context.SaveChanges();
    }

    private PartnerAvailabilityService CreateService(NestlyDbContext context) => new(
        new PartnerRepository(context),
        new PartnerAvailabilityWindowRepository(context),
        new PartnerBlackoutDateRepository(context));

    [Fact]
    public async Task GetAsync_returns_empty_collections_when_nothing_is_set()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetAsync(_partnerId);

        result.Windows.Should().BeEmpty();
        result.BlackoutDates.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateWindowsAsync_replaces_the_whole_weekly_schedule()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        await service.UpdateWindowsAsync(_partnerId, new UpdatePartnerAvailabilityWindowsRequest(
            [new PartnerAvailabilityWindowInput(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17))]));

        var result = await service.UpdateWindowsAsync(_partnerId, new UpdatePartnerAvailabilityWindowsRequest(
            [
                new PartnerAvailabilityWindowInput(DayOfWeek.Tuesday, TimeSpan.FromHours(10), TimeSpan.FromHours(18)),
                new PartnerAvailabilityWindowInput(DayOfWeek.Wednesday, TimeSpan.FromHours(10), TimeSpan.FromHours(18))
            ]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var stored = await context.Set<PartnerAvailabilityWindow>().Where(w => w.PartnerId == _partnerId).ToListAsync();
        stored.Should().HaveCount(2);
        stored.Should().NotContain(w => w.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task UpdateWindowsAsync_rejects_an_unknown_partner()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).UpdateWindowsAsync(Guid.NewGuid(), new UpdatePartnerAvailabilityWindowsRequest(
            [new PartnerAvailabilityWindowInput(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17))]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerAvailability.NotFound");
    }

    [Fact]
    public async Task AddBlackoutDateAsync_stores_the_blackout_date()
    {
        await using var context = _database.CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await CreateService(context).AddBlackoutDateAsync(
            _partnerId, new AddPartnerBlackoutDateRequest(today, today.AddDays(3), "Personal leave"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be("Personal leave");

        var stored = await context.Set<PartnerBlackoutDate>().SingleAsync();
        stored.PartnerId.Should().Be(_partnerId);
    }

    [Fact]
    public async Task DeleteBlackoutDateAsync_removes_the_blackout_date()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var added = await service.AddBlackoutDateAsync(_partnerId, new AddPartnerBlackoutDateRequest(today, today, null));

        var result = await service.DeleteBlackoutDateAsync(_partnerId, added.Value.Id);

        result.IsSuccess.Should().BeTrue();
        (await context.Set<PartnerBlackoutDate>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBlackoutDateAsync_rejects_a_blackout_date_belonging_to_another_partner()
    {
        await using var context = _database.CreateContext();
        var otherPartner = new Partner(Guid.NewGuid(), "Other Partner", "Other's Services", PartnerType.Individual, "+919876500000");
        context.Add(otherPartner);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var added = await service.AddBlackoutDateAsync(otherPartner.Id, new AddPartnerBlackoutDateRequest(today, today, null));

        var result = await service.DeleteBlackoutDateAsync(_partnerId, added.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerAvailability.BlackoutDateNotFound");
    }

    public void Dispose() => _database.Dispose();
}
