using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IAssignmentResponseExpirySweepJob"/>.</summary>
public class AssignmentResponseExpirySweepJob : IAssignmentResponseExpirySweepJob
{
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly IBookingProviderAssignmentService _assignmentService;
    private readonly ILogger<AssignmentResponseExpirySweepJob> _logger;

    public AssignmentResponseExpirySweepJob(
        IBookingProviderAssignmentRepository assignmentRepository,
        IBookingProviderAssignmentService assignmentService,
        ILogger<AssignmentResponseExpirySweepJob> logger)
    {
        _assignmentRepository = assignmentRepository;
        _assignmentService = assignmentService;
        _logger = logger;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var stale = await _assignmentRepository.ListUnansweredPastDeadlineAsync(DateTime.UtcNow);

        int expiredCount = 0;
        foreach (var assignment in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ExpireAsync re-checks Status itself (still Assigned?) before
            // acting, since the provider may have responded in the gap
            // between this list query and reaching their row - the same
            // "re-verify right before writing" shape BookingExpirySweepJob's
            // ListStalePaymentPendingAsync callers don't need (a booking's
            // PaymentPending window has no other writer racing it) but this
            // one does, because AcceptAsync/RejectByProviderAsync can commit
            // at any moment on a live assignment.
            var result = await _assignmentService.ExpireAsync(assignment.Id);
            if (result.IsSuccess && result.Value.Status == BookingProviderAssignmentStatus.Expired)
            {
                expiredCount++;
            }
        }

        _logger.LogInformation("Assignment response-expiry sweep: {ExpiredCount} unanswered assignment(s) expired.", expiredCount);
    }
}
