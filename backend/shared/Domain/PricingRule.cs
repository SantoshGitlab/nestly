using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Represents a pricing rule with discount type, amount, and applicable conditions.
/// </summary>
public class PricingRule : Entity<Guid>
{
    public string DiscountType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string ApplicableConditions { get; private set; } = string.Empty;

    protected PricingRule() { }

    public PricingRule(Guid id, string discountType, decimal amount, string applicableConditions)
        : base(id)
    {
        DiscountType = discountType ?? throw new ArgumentNullException(nameof(discountType));
        Amount = amount;
        ApplicableConditions = applicableConditions ?? throw new ArgumentNullException(nameof(applicableConditions));
    }

    public void UpdateDiscount(string discountType, decimal amount) => (DiscountType, Amount) = (discountType, amount);
    public void UpdateApplicableConditions(string applicableConditions) => ApplicableConditions = applicableConditions ?? throw new ArgumentNullException(nameof(applicableConditions));
}
