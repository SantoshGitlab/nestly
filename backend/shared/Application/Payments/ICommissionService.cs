namespace Nestly.Application.Payments;

/// <summary>The resolved rate and computed amount for one booking's settlement (task 157).</summary>
public record CommissionCalculationResult(decimal RatePercentage, decimal CommissionAmount);

/// <summary>
/// Resolves the platform commission rate that applies to a booking's
/// settlement - the admin-configurable global rate, or a per-category
/// override when one is configured (task 157) - and computes the
/// commission amount for it.
/// </summary>
public interface ICommissionService
{
    /// <summary>
    /// <paramref name="categoryId"/> is the booking's (single, today - see
    /// <see cref="Nestly.Domain.Booking"/>'s doc comment) service category,
    /// when it could be resolved; null falls back to the global default rate.
    /// </summary>
    CommissionCalculationResult Calculate(decimal payableAmount, Guid? categoryId);
}
