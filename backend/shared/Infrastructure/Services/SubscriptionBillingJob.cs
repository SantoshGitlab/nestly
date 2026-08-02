using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Payments;
using Nestly.Application.Subscriptions;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="ISubscriptionBillingJob"/>.</summary>
public class SubscriptionBillingJob : ISubscriptionBillingJob
{
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ISandboxPaymentSimulator _paymentSimulator;
    private readonly SubscriptionBillingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubscriptionBillingJob> _logger;

    public SubscriptionBillingJob(
        ICustomerSubscriptionRepository subscriptionRepository,
        IPaymentGateway paymentGateway,
        ISandboxPaymentSimulator paymentSimulator,
        IOptions<SubscriptionBillingOptions> options,
        TimeProvider timeProvider,
        ILogger<SubscriptionBillingJob> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _paymentGateway = paymentGateway;
        _paymentSimulator = paymentSimulator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessDueBillingAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await ChargeDueSubscriptionsAsync(nowUtc, cancellationToken);
        await NotifyExpiringSoonAsync(nowUtc, cancellationToken);
    }

    private async Task ChargeDueSubscriptionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var due = await _subscriptionRepository.ListDueForBillingAsync(nowUtc);

        int succeeded = 0, failed = 0;
        foreach (var subscription in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reuses the existing sandbox payment gateway interface (task
            // 68a) rather than a new integration, as PRODUCT-ENHANCEMENTS.md
            // #1 requires. This is an off-session, server-initiated charge -
            // there is no customer present to complete a redirect/webhook
            // round trip the way checkout's IPaymentService does - so the
            // job creates the order and resolves its outcome synchronously
            // in one step via ISandboxPaymentSimulator, the same
            // deterministic outcome function the checkout webhook simulator
            // (PaymentService.SimulateAsync) already uses. A production
            // integration would replace this with the vendor's actual
            // off-session/saved-payment-method charge API behind the same
            // IPaymentGateway seam.
            var order = await _paymentGateway.CreateOrderAsync(
                new GatewayCreateOrderRequest(subscription.Id, subscription.PriceSnapshot, "INR", subscription.Id.ToString("N")),
                cancellationToken);
            var outcome = _paymentSimulator.DetermineOutcome(subscription.PriceSnapshot);

            if (outcome.Succeeded)
            {
                subscription.RecordSuccessfulRenewal(nowUtc);
                succeeded++;
            }
            else
            {
                subscription.RecordFailedCharge(
                    nowUtc,
                    outcome.FailureReason ?? "Payment declined.",
                    _options.RetryLimit,
                    TimeSpan.FromDays(_options.RetryBackoffDays));
                failed++;
            }

            await _subscriptionRepository.UpdateAsync(subscription);
            _ = order; // Gateway order id has no further use once the outcome is resolved synchronously above.
        }

        _logger.LogInformation(
            "Subscription billing sweep: {DueCount} subscription(s) due, {SucceededCount} renewed, {FailedCount} failed.",
            due.Count, succeeded, failed);
    }

    private async Task NotifyExpiringSoonAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var windowEndUtc = nowUtc.AddDays(_options.ExpiringSoonLeadTimeDays);
        var expiringSoon = await _subscriptionRepository.ListExpiringSoonAsync(nowUtc, windowEndUtc);

        foreach (var subscription in expiringSoon)
        {
            cancellationToken.ThrowIfCancellationRequested();

            subscription.MarkExpiringSoonNotified();
            await _subscriptionRepository.UpdateAsync(subscription);
        }

        if (expiringSoon.Count > 0)
        {
            _logger.LogInformation("Subscription billing sweep: {Count} subscription(s) notified as expiring soon.", expiringSoon.Count);
        }
    }
}
