namespace Nestly.BuildingBlocks.Primitives;

/// <summary>
/// Base class for entities: objects with identity that persists across state changes.
/// Equality is based solely on <see cref="Id"/>.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>EF Core materialization constructor.</summary>
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; protected set; }

    public bool Equals(Entity<TId>? other)
    {
        return other is not null && GetType() == other.GetType() && Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
