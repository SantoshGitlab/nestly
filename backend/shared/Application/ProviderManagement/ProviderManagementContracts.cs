using Nestly.Domain;

namespace Nestly.Application.ProviderManagement;

// ---- CRUD (task 150a) ----

/// <summary>Search/filter criteria for the admin provider list (mirrors <c>CustomerSearchFilter</c>).</summary>
public sealed record ProviderSearchFilter(
    string? Name,
    string? Phone,
    ProviderStatus? Status,
    ProviderOnboardingStatus? OnboardingStatus,
    int Page,
    int PageSize);

public sealed record ProviderSearchResult(IReadOnlyList<Provider> Rows, int TotalCount);

public sealed record ProviderSearchRequest(
    string? Name,
    string? Phone,
    ProviderStatus? Status,
    ProviderOnboardingStatus? OnboardingStatus,
    int Page = 1,
    int PageSize = 20);

public sealed record ProviderSummaryResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    ProviderStatus Status,
    ProviderOnboardingStatus OnboardingStatus,
    DateTime CreatedAt);

public sealed record ProviderSearchResponse(IReadOnlyList<ProviderSummaryResponse> Items, int TotalCount, int Page, int PageSize);

/// <summary>Admin creates a provider record directly (as opposed to the provider's own self-service registration, task 146a). ProviderType is always Individual - OPEN DECISIONS #2.</summary>
public sealed record CreateProviderRequest(string LegalName, string DisplayName, string Phone, string? Email);

/// <param name="Latitude">
/// Task 243: both-or-neither with <paramref name="Longitude"/>, feeding the
/// automatic-assignment engine's distance ranking (task 244). Full-overwrite
/// semantics, same as every other field on this request (this is a PUT-style
/// update, not a patch) - submitting both as null clears a previously set
/// location.
/// </param>
public sealed record UpdateProviderRequest(string LegalName, string DisplayName, string? Email, decimal? Latitude = null, decimal? Longitude = null);

public sealed record SuspendProviderRequest(string Reason);

public sealed record ProviderKycDocumentResponse(
    Guid Id,
    ProviderKycDocumentType DocType,
    string? DocNumber,
    string FileRef,
    ProviderKycVerificationStatus VerificationStatus,
    Guid? VerifiedBy,
    DateTime? VerifiedAt,
    DateTime SubmittedAt);

public sealed record ProviderBackgroundCheckResponse(
    Guid Id,
    ProviderBackgroundCheckStatus Status,
    Guid CheckedBy,
    DateTime CheckedAt,
    string? Notes);

/// <summary>
/// Full admin provider detail (task 150a/150b): profile plus KYC documents
/// and background check history for the approval workflow, plus the profile
/// photo and its moderation state (task 293).
/// </summary>
/// <param name="Photo">
/// Appended last on purpose: this is a positional record, and inserting a
/// parameter mid-list would silently re-bind every argument after it at the
/// one call site (<c>ProviderDetailMapper</c>) that builds it.
/// </param>
public sealed record ProviderDetailResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    ProviderType ProviderType,
    string Phone,
    string? Email,
    ProviderStatus Status,
    ProviderOnboardingStatus OnboardingStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<ProviderKycDocumentResponse> KycDocuments,
    IReadOnlyList<ProviderBackgroundCheckResponse> BackgroundChecks,
    ProviderPhotoResponse Photo);

// ---- Photo moderation (task 293) ----

/// <summary>
/// One provider's profile photo and where it stands with moderation (task
/// 293) - the row the admin queue renders and the shape every verdict
/// returns.
/// </summary>
/// <param name="PhotoUrl">Deliberately the raw stored reference, not <see cref="Provider.PublicPhotoUrl"/>: a moderator has to see the photo precisely because it has NOT been approved.</param>
public sealed record ProviderPhotoResponse(
    Guid ProviderId,
    string DisplayName,
    string? PhotoUrl,
    ProviderPhotoModerationStatus? ModerationStatus,
    Guid? ModeratedByAdminUserId,
    DateTime? ModeratedAtUtc,
    string? ModerationNote);

/// <summary>A rejection must say why - the note is shown back to the provider so a rejected photo is actionable rather than a silent dead end (mirrors <see cref="RejectProviderKycDocumentRequest"/>).</summary>
public sealed record RejectProviderPhotoRequest(string Reason);

// ---- KYC approval and activation (task 150b, 160) ----

public sealed record RejectProviderKycDocumentRequest(string Reason);

public sealed record RecordBackgroundCheckRequest(ProviderBackgroundCheckStatus Status, string? Notes);

// ---- Performance view (task 150c) ----

/// <summary>
/// A provider's job-fulfilment performance summary (PROVIDER.md API surface
/// "get provider performance metrics"). Built from <see cref="Booking"/>/<see cref="BookingProviderAssignment"/>
/// history rather than a new rollup table - <c>provider_rating_summary</c> is
/// out of this pass's scope (PROVIDER.md OPEN DECISIONS #4: rating does not
/// affect assignment, and no review-to-provider link exists yet).
/// </summary>
public sealed record ProviderPerformanceResponse(
    Guid ProviderId,
    int TotalAssignments,
    int AcceptedAssignments,
    int RejectedAssignments,
    int CompletedJobs,
    int InProgressJobs,
    decimal LifetimeEarnings);
