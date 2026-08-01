using Nestly.Domain;

namespace Nestly.Application.PartnerManagement;

// ---- CRUD (task 150a) ----

/// <summary>Search/filter criteria for the admin partner list (mirrors <c>CustomerSearchFilter</c>).</summary>
public sealed record PartnerSearchFilter(
    string? Name,
    string? Phone,
    PartnerStatus? Status,
    PartnerOnboardingStatus? OnboardingStatus,
    int Page,
    int PageSize);

public sealed record PartnerSearchResult(IReadOnlyList<Partner> Rows, int TotalCount);

public sealed record PartnerSearchRequest(
    string? Name,
    string? Phone,
    PartnerStatus? Status,
    PartnerOnboardingStatus? OnboardingStatus,
    int Page = 1,
    int PageSize = 20);

public sealed record PartnerSummaryResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    PartnerStatus Status,
    PartnerOnboardingStatus OnboardingStatus,
    DateTime CreatedAt);

public sealed record PartnerSearchResponse(IReadOnlyList<PartnerSummaryResponse> Items, int TotalCount, int Page, int PageSize);

/// <summary>Admin creates a partner record directly (as opposed to the partner's own self-service registration, task 146a). PartnerType is always Individual - OPEN DECISIONS #2.</summary>
public sealed record CreatePartnerRequest(string LegalName, string DisplayName, string Phone, string? Email);

public sealed record UpdatePartnerRequest(string LegalName, string DisplayName, string? Email);

public sealed record SuspendPartnerRequest(string Reason);

public sealed record PartnerKycDocumentResponse(
    Guid Id,
    PartnerKycDocumentType DocType,
    string? DocNumber,
    string FileRef,
    PartnerKycVerificationStatus VerificationStatus,
    Guid? VerifiedBy,
    DateTime? VerifiedAt,
    DateTime SubmittedAt);

public sealed record PartnerBackgroundCheckResponse(
    Guid Id,
    PartnerBackgroundCheckStatus Status,
    Guid CheckedBy,
    DateTime CheckedAt,
    string? Notes);

/// <summary>Full admin partner detail (task 150a/150b): profile plus KYC documents and background check history for the approval workflow.</summary>
public sealed record PartnerDetailResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    PartnerType PartnerType,
    string Phone,
    string? Email,
    PartnerStatus Status,
    PartnerOnboardingStatus OnboardingStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PartnerKycDocumentResponse> KycDocuments,
    IReadOnlyList<PartnerBackgroundCheckResponse> BackgroundChecks);

// ---- KYC approval and activation (task 150b, 160) ----

public sealed record RejectPartnerKycDocumentRequest(string Reason);

public sealed record RecordBackgroundCheckRequest(PartnerBackgroundCheckStatus Status, string? Notes);

// ---- Performance view (task 150c) ----

/// <summary>
/// A partner's job-fulfilment performance summary (PARTNER.md API surface
/// "get partner performance metrics"). Built from <see cref="Booking"/>/<see cref="BookingPartnerAssignment"/>
/// history rather than a new rollup table - <c>partner_rating_summary</c> is
/// out of this pass's scope (PARTNER.md OPEN DECISIONS #4: rating does not
/// affect assignment, and no review-to-partner link exists yet).
/// </summary>
public sealed record PartnerPerformanceResponse(
    Guid PartnerId,
    int TotalAssignments,
    int AcceptedAssignments,
    int RejectedAssignments,
    int CompletedJobs,
    int InProgressJobs,
    decimal LifetimeEarnings);
