namespace Nestly.Application.Bookings;

/// <summary>
/// One checklist item's answer, as submitted by the partner (task 197).
/// Mirrors <see cref="Nestly.Domain.CompletionChecklistAnswer"/> - kept as a
/// separate Application-layer shape per this codebase's Domain/Application
/// layering rule (Domain has no reference to Application).
/// </summary>
public sealed record CompletionChecklistAnswerRequest(string Item, bool Completed, string? Notes);

/// <summary>
/// Partner-side capture of job completion evidence (task 197): one or more
/// photo references (already-uploaded storage keys/URLs - the same
/// reference-only convention <see cref="Nestly.Application.PartnerJobs.UploadJobCompletionProofRequest"/>
/// and <c>PartnerKycDocument.FileRef</c> already use, not a binary upload)
/// plus the checklist the partner walked through on site.
/// </summary>
public sealed record SubmitCompletionProofRequest(
    IReadOnlyList<string> PhotoRefs,
    IReadOnlyList<CompletionChecklistAnswerRequest> ChecklistAnswers);

public sealed record CompletionChecklistAnswerResponse(string Item, bool Completed, string? Notes);

/// <summary>
/// The full completion proof for a booking (tasks 195-198) - shown to the
/// partner after submission, the customer on their order detail (task 198,
/// SRS 11.13), and the admin during dispute review (task 198, SRS 12.11.2).
/// The same record serves all three call sites; there is only one record of
/// "what happened" (PRODUCT-ENHANCEMENTS.md "Visibility").
/// </summary>
public sealed record BookingCompletionProofResponse(
    Guid Id,
    Guid BookingId,
    IReadOnlyList<string> PhotoRefs,
    IReadOnlyList<CompletionChecklistAnswerResponse> ChecklistAnswers,
    Guid SubmittedByPartnerId,
    DateTime SubmittedAtUtc);
