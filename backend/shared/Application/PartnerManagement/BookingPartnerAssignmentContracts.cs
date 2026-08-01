using Nestly.Domain;

namespace Nestly.Application.PartnerManagement;

/// <summary>Admin assigns a partner to a booking (task 147, PARTNER.md OPEN DECISIONS #1 - manual, admin-driven).</summary>
public sealed record AssignPartnerRequest(Guid PartnerId, DateTime? ResponseDeadline);

/// <summary>Rejects the booking's current outstanding assignment (task 159). An admin may record this on the partner's behalf (e.g. a phone-call decline); the same service method is what a future partner-facing reject endpoint (task 151) would call.</summary>
public sealed record RejectAssignmentRequest(string? Reason);

public sealed record BookingPartnerAssignmentResponse(
    Guid Id,
    Guid BookingId,
    Guid PartnerId,
    string PartnerDisplayName,
    BookingAssignedByType AssignedByType,
    Guid? AssignedByUserId,
    DateTime AssignedAt,
    BookingPartnerAssignmentStatus Status,
    DateTime? ResponseDeadline,
    DateTime? RespondedAt,
    string? Notes);
