using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Decides how the destinations of one route-estimate request are split
/// across <c>computeRouteMatrix</c> calls, and where the request stops being
/// worth billing (task 266). Separated from
/// <see cref="GoogleMapsRouteEstimateProvider"/> because it is pure
/// arithmetic over two configured limits - keeping it here makes both the
/// chunking rule and the fan-out cap testable without an HTTP stub.
/// </summary>
public static class RouteMatrixBatchPlanner
{
    /// <summary>
    /// Splits <paramref name="destinationIndexes"/> into per-request batches.
    /// </summary>
    /// <param name="destinationIndexes">
    /// Positions, in the caller's destination list, that still need an
    /// answer - typically the cache misses.
    /// </param>
    /// <param name="options">Supplies the per-call and per-estimate limits.</param>
    /// <returns>
    /// The batches to send, plus the destinations beyond the fan-out cap. The
    /// two together always account for every input index: an over-cap request
    /// is trimmed, never rejected, because the caller is still owed one
    /// estimate per destination.
    /// </returns>
    public static RouteMatrixBatchPlan Plan(IReadOnlyList<int> destinationIndexes, GoogleMapsOptions options)
    {
        ArgumentNullException.ThrowIfNull(destinationIndexes);
        ArgumentNullException.ThrowIfNull(options);

        // Clamped rather than trusted: these come from configuration, and a
        // hand-edited appsettings that slips past DataAnnotations validation
        // must not be able to emit a request Google will reject outright.
        int maxPerCall = Math.Clamp(options.MaxDestinationsPerCall, 1, GoogleMapsOptions.MaxElementsPerRequestLimit);
        int maxPerEstimate = Math.Max(options.MaxDestinationsPerEstimate, 1);

        int routedCount = Math.Min(destinationIndexes.Count, maxPerEstimate);

        var batches = new List<IReadOnlyList<int>>();
        for (int offset = 0; offset < routedCount; offset += maxPerCall)
        {
            int length = Math.Min(maxPerCall, routedCount - offset);
            var batch = new int[length];
            for (int position = 0; position < length; position++)
            {
                batch[position] = destinationIndexes[offset + position];
            }

            batches.Add(batch);
        }

        var overflow = new int[destinationIndexes.Count - routedCount];
        for (int position = 0; position < overflow.Length; position++)
        {
            overflow[position] = destinationIndexes[routedCount + position];
        }

        return new RouteMatrixBatchPlan(batches, overflow);
    }
}

/// <summary>
/// The outcome of <see cref="RouteMatrixBatchPlanner.Plan"/>.
/// </summary>
/// <param name="Batches">Destination indexes grouped into one HTTP request each.</param>
/// <param name="Overflow">Destination indexes dropped by the fan-out cap, to be answered by the sandbox estimator.</param>
public sealed record RouteMatrixBatchPlan(
    IReadOnlyList<IReadOnlyList<int>> Batches,
    IReadOnlyList<int> Overflow);
