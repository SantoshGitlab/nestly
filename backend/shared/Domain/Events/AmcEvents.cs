using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

/// <summary>A customer purchased a new AMC contract (docs/AMC.md).</summary>
public sealed record AmcContractPurchasedEvent(Guid ContractId, Guid CustomerId) : DomainEvent;

/// <summary>A visit was redeemed against a contract's entitlement, on booking completion (docs/AMC.md - entitlement decrements on completion, not on booking creation).</summary>
public sealed record AmcVisitRedeemedEvent(Guid ContractId, Guid CustomerId, Guid BookingId, int VisitsRemaining) : DomainEvent;

/// <summary>The contract's term end date is within the reminder window (docs/AMC.md renewal pipeline).</summary>
public sealed record AmcContractExpiringSoonEvent(Guid ContractId, Guid CustomerId) : DomainEvent;

/// <summary>Every entitled visit has been redeemed while the term still has time left - a success, not a failure; see <see cref="CustomerAmcContractStatus"/>'s doc comment for why this is reported separately from expiry.</summary>
public sealed record AmcContractExhaustedEvent(Guid ContractId, Guid CustomerId) : DomainEvent;
