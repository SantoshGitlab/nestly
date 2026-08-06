using FluentAssertions;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Services;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 266's batching rules: destinations are chunked into
/// API-sized requests, and one estimate can never fan out into an unbounded
/// number of billed elements. Pure arithmetic, so no HTTP stub is involved -
/// exercising the same limits through a stubbed handler is task 286.
/// </summary>
public sealed class RouteMatrixBatchPlannerTests
{
    private static GoogleMapsOptions Options(int maxDestinationsPerCall, int maxDestinationsPerEstimate = 100) =>
        new()
        {
            MaxDestinationsPerCall = maxDestinationsPerCall,
            MaxDestinationsPerEstimate = maxDestinationsPerEstimate
        };

    private static int[] Indexes(int count) => [.. Enumerable.Range(0, count)];

    [Fact]
    public void Plan_produces_no_batches_for_an_empty_request()
    {
        var plan = RouteMatrixBatchPlanner.Plan([], Options(maxDestinationsPerCall: 25));

        plan.Batches.Should().BeEmpty();
        plan.Overflow.Should().BeEmpty();
    }

    [Fact]
    public void Plan_sends_one_request_when_every_destination_fits()
    {
        var plan = RouteMatrixBatchPlanner.Plan(Indexes(10), Options(maxDestinationsPerCall: 25));

        plan.Batches.Should().ContainSingle();
        plan.Batches[0].Should().Equal(Indexes(10));
        plan.Overflow.Should().BeEmpty();
    }

    [Fact]
    public void Plan_chunks_destinations_at_the_configured_call_size()
    {
        var plan = RouteMatrixBatchPlanner.Plan(Indexes(7), Options(maxDestinationsPerCall: 3));

        plan.Batches.Select(batch => batch.Count).Should().Equal(3, 3, 1);
        plan.Batches.SelectMany(batch => batch).Should().Equal(Indexes(7));
    }

    [Fact]
    public void Plan_never_exceeds_googles_element_limit_even_when_configured_higher()
    {
        // A hand-edited appsettings could slip a larger number past validation;
        // Google would then reject the whole request rather than trim it.
        var plan = RouteMatrixBatchPlanner.Plan(
            Indexes(250),
            Options(maxDestinationsPerCall: 1_000, maxDestinationsPerEstimate: 500));

        plan.Batches.Should().OnlyContain(batch => batch.Count <= GoogleMapsOptions.MaxElementsPerRequestLimit);
    }

    [Fact]
    public void Plan_treats_a_non_positive_call_size_as_one_destination_per_request()
    {
        var plan = RouteMatrixBatchPlanner.Plan(Indexes(3), Options(maxDestinationsPerCall: 0));

        plan.Batches.Select(batch => batch.Count).Should().Equal(1, 1, 1);
    }

    [Fact]
    public void Plan_moves_destinations_beyond_the_fan_out_cap_into_overflow()
    {
        var plan = RouteMatrixBatchPlanner.Plan(
            Indexes(30),
            Options(maxDestinationsPerCall: 25, maxDestinationsPerEstimate: 12));

        plan.Batches.SelectMany(batch => batch).Should().HaveCount(12);
        plan.Overflow.Should().Equal([.. Enumerable.Range(12, 18)]);
    }

    [Fact]
    public void Plan_accounts_for_every_requested_destination_exactly_once()
    {
        // The caller is owed one estimate per destination, so an over-cap
        // request is trimmed to the sandbox estimator, never silently dropped.
        var plan = RouteMatrixBatchPlanner.Plan(
            Indexes(40),
            Options(maxDestinationsPerCall: 7, maxDestinationsPerEstimate: 20));

        plan.Batches.SelectMany(batch => batch).Concat(plan.Overflow)
            .Should().BeEquivalentTo(Indexes(40));
    }

    [Fact]
    public void Plan_preserves_the_callers_destination_indexes()
    {
        // Cache hits leave gaps, so the pending list is rarely contiguous and
        // the batches must carry the original positions, not 0..n.
        int[] pending = [3, 7, 9, 14];

        var plan = RouteMatrixBatchPlanner.Plan(pending, Options(maxDestinationsPerCall: 2));

        plan.Batches.Should().HaveCount(2);
        plan.Batches[0].Should().Equal(3, 7);
        plan.Batches[1].Should().Equal(9, 14);
    }
}
