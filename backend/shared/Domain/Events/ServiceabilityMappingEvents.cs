using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record CategoryCityMappingChangedEvent(Guid CategoryId, Guid CityId) : DomainEvent;

public sealed record ServicePincodeMappingChangedEvent(Guid ServiceId, Guid PincodeId) : DomainEvent;
