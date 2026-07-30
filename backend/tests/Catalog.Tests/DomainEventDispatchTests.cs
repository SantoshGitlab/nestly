using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 40e (dispatch half): DomainEventDispatchInterceptor actually
/// publishes raised domain events through MediatR once SaveChangesAsync
/// commits, and clears them off the aggregate afterwards.
/// </summary>
public sealed class DomainEventDispatchTests
{
    private sealed class CategoryCreatedHandler : INotificationHandler<DomainEventNotification<CategoryCreatedEvent>>
    {
        public static readonly List<CategoryCreatedEvent> Received = [];

        public Task Handle(DomainEventNotification<CategoryCreatedEvent> notification, CancellationToken cancellationToken)
        {
            Received.Add(notification.DomainEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Saving_a_new_category_publishes_CategoryCreatedEvent_and_clears_it_from_the_aggregate()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DomainEventDispatchTests).Assembly));
        services.AddSingleton<DomainEventDispatchInterceptor>();
        await using var provider = services.BuildServiceProvider();
        var interceptor = provider.GetRequiredService<DomainEventDispatchInterceptor>();

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<NestlyDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        using var context = new NestlyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var category = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        context.Add(category);

        await context.SaveChangesAsync();

        CategoryCreatedHandler.Received.Should().Contain(e => e.CategoryId == category.Id);
        category.DomainEvents.Should().BeEmpty();
    }
}
