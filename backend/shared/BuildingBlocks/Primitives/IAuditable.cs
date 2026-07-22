namespace Nestly.BuildingBlocks.Primitives;

/// <summary>
/// Entities implementing this interface get audit columns populated automatically
/// by the persistence layer.
/// </summary>
public interface IAuditable
{
    DateTime CreatedOnUtc { get; set; }

    DateTime? ModifiedOnUtc { get; set; }
}
