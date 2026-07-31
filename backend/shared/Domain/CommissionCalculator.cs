namespace Nestly.Domain;

/// <summary>
/// Pure commission-amount math (task 157). Rate *resolution* - global default
/// vs. an admin-configured per-category override - lives in Infrastructure's
/// CommissionOptions/ICommissionService, since that needs configuration
/// binding this layer must not depend on. This class only turns an
/// already-resolved rate + payable amount into a rupee-and-paise commission
/// amount, so the arithmetic itself can be unit tested with no DI/config
/// plumbing at all.
/// </summary>
public static class CommissionCalculator
{
    /// <summary>
    /// Commission = payableAmount * ratePercentage / 100, rounded to 2
    /// decimal places (paise) with banker's rounding - consistent with how
    /// every other money column in this codebase is stored
    /// (<c>HasPrecision(12, 2)</c>).
    /// </summary>
    public static decimal Calculate(decimal payableAmount, decimal ratePercentage)
    {
        if (payableAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payableAmount), "Payable amount cannot be negative.");
        }

        if (ratePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercentage), "Commission rate must be between 0 and 100.");
        }

        return Math.Round(payableAmount * ratePercentage / 100m, 2, MidpointRounding.ToEven);
    }
}
