namespace Nestly.Application.Abstractions.Caching;

/// <summary>
/// Central cache-key vocabulary (T017). Keys are built here rather than
/// inlined at call sites so invalidation stays deterministic: the code that
/// writes an entry and the code that evicts it derive the same string from the
/// same method (docs/DOTNET.md — "Cache invalidation should be deterministic").
/// </summary>
/// <remarks>
/// Format is <c>nestly:{area}:{identifier}</c>. The shared prefix keeps Nestly
/// entries distinguishable when a Redis instance is shared with another
/// workload, and makes the key space greppable in redis-cli.
/// </remarks>
public static class CacheKeys
{
    private const string Prefix = "nestly";

    /// <summary>Cache areas, one per invalidation boundary.</summary>
    public static class Areas
    {
        public const string Catalog = "catalog";
        public const string Session = "session";
    }

    /// <summary>A single service's catalog projection.</summary>
    public static string Service(Guid serviceId) =>
        Compose(Areas.Catalog, "service", serviceId.ToString("D"));

    /// <summary>A single category's catalog projection.</summary>
    public static string Category(Guid categoryId) =>
        Compose(Areas.Catalog, "category", categoryId.ToString("D"));

    /// <summary>The list of services belonging to a category.</summary>
    public static string ServicesByCategory(Guid categoryId) =>
        Compose(Areas.Catalog, "category", categoryId.ToString("D"), "services");

    /// <summary>A customer's active session projection.</summary>
    public static string CustomerSession(Guid customerId) =>
        Compose(Areas.Session, "customer", customerId.ToString("D"));

    /// <summary>
    /// Builds a key from pre-validated segments. Kept private so every key in
    /// the system originates from one of the named methods above.
    /// </summary>
    private static string Compose(params string[] segments) =>
        string.Join(':', segments.Prepend(Prefix));
}
