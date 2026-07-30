namespace Nestly.Application.Payments;

/// <summary>
/// Sandbox-only capability, deliberately separate from <see cref="IPaymentGateway"/>:
/// a real gateway has no "decide the outcome for me" API - the customer's
/// bank/card does that. This lets the sandbox "simulate payment" endpoint
/// (task 68b) ask, in one explicit, self-documenting call, what a sandbox
/// order should resolve to, without leaking that sandbox-only concern into
/// the vendor-agnostic <see cref="IPaymentGateway"/> abstraction.
/// </summary>
public interface ISandboxPaymentSimulator
{
    /// <summary>
    /// Deterministic outcome for completing payment on a sandbox order of
    /// <paramref name="amount"/> (see <c>SandboxPaymentGateway</c> for the
    /// exact convention). Never random - QA (task 79) and any caller must be
    /// able to force either outcome on demand by choosing the amount.
    /// </summary>
    SandboxPaymentOutcome DetermineOutcome(decimal amount);
}

public sealed record SandboxPaymentOutcome(bool Succeeded, string GatewayPaymentRef, string? FailureReason);
