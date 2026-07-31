using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Payments;

/// <summary>
/// Handles the gateway's payment callback (SRS 30.1, 11.11.3, tasks 69a-c):
/// signature verification, idempotent duplicate handling, and applying the
/// outcome to the booking-payment mapping. Not scoped to a customer id - a
/// webhook is called by the gateway itself, authenticated by its signature
/// rather than a bearer token (SRS 28.3 "payment callback abuse").
/// </summary>
public interface IPaymentWebhookService
{
    Task<Result> HandleCallbackAsync(PaymentWebhookRequest request);
}
