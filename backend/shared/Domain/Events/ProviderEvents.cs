using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

/// <summary>An admin approved one of this provider's submitted KYC documents (e.g. ID proof, address proof).</summary>
public sealed record ProviderKycDocumentApprovedEvent(Guid ProviderId, Guid DocumentId, ProviderKycDocumentType DocType) : DomainEvent;

/// <summary>An admin rejected one of this provider's submitted KYC documents.</summary>
public sealed record ProviderKycDocumentRejectedEvent(Guid ProviderId, Guid DocumentId, ProviderKycDocumentType DocType) : DomainEvent;

/// <summary>An admin activated this provider (go-live) once KYC and the background check both cleared.</summary>
public sealed record ProviderActivatedEvent(Guid ProviderId) : DomainEvent;
