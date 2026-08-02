using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Bookings;

/// <summary>
/// Shared helpers for the two things every caller touching
/// <see cref="BookingCompletionProof"/> needs (tasks 196, 198):
/// <list type="bullet">
/// <item>the task 196 guard - both writers of <see cref="BookingStatus.Completed"/>
/// (<c>PartnerJobService.CompleteAsync</c> and
/// <c>BookingManagementService.UpdateStatusAsync</c>) call
/// <see cref="EnsureCompletionProofExistsAsync"/> so "no proof, no
/// Completed" is enforced once rather than re-implemented per caller;</item>
/// <item>the read-side mapping every one of the three surfaces (partner,
/// customer, admin - task 198) needs to show the same proof.</item>
/// </list>
/// </summary>
public static class BookingCompletionProofSupport
{
    /// <summary>Null if a completion proof exists for the booking; otherwise the business error the caller should return instead of transitioning to Completed.</summary>
    public static async Task<Error?> EnsureCompletionProofExistsAsync(this IBookingCompletionProofRepository repository, Guid bookingId)
    {
        var exists = await repository.ExistsForBookingAsync(bookingId);
        return exists
            ? null
            : Error.Business(
                "Booking.CompletionProofRequired",
                "This booking cannot be marked Completed until a completion proof (photos and checklist) has been submitted.");
    }

    public static BookingCompletionProofResponse? ToResponse(this BookingCompletionProof? proof) =>
        proof is null
            ? null
            : new BookingCompletionProofResponse(
                proof.Id,
                proof.BookingId,
                proof.PhotoRefs,
                proof.ChecklistAnswers.Select(a => new CompletionChecklistAnswerResponse(a.Item, a.Completed, a.Notes)).ToList(),
                proof.SubmittedByPartnerId,
                proof.SubmittedAtUtc);
}
