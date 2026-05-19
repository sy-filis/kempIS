using SharedKernel;

namespace Domain.Reservations.Vehicles;

public sealed record VehicleCreatedDomainEvent(Guid VehicleId) : IDomainEvent;
