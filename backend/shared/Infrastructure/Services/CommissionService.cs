using Microsoft.Extensions.Options;
using Nestly.Application.Payments;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Resolves the commission rate from <see cref="CommissionOptions"/> (task
/// 157: global rate, with an optional per-category override) and applies
/// <see cref="CommissionCalculator"/> to it. Stateless - safe as a singleton,
/// same as <see cref="SandboxPaymentGateway"/> - since it only reads bound
/// Options and does no I/O of its own.
/// </summary>
public class CommissionService : ICommissionService
{
    private readonly IOptions<CommissionOptions> _options;

    public CommissionService(IOptions<CommissionOptions> options)
    {
        _options = options;
    }

    public CommissionCalculationResult Calculate(decimal payableAmount, Guid? categoryId)
    {
        var options = _options.Value;
        decimal rate = options.DefaultRatePercentage;

        if (categoryId is not null &&
            options.CategoryRateOverrides.TryGetValue(categoryId.Value.ToString(), out decimal overrideRate))
        {
            rate = overrideRate;
        }

        decimal commissionAmount = CommissionCalculator.Calculate(payableAmount, rate);
        return new CommissionCalculationResult(rate, commissionAmount);
    }
}
