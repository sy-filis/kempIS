namespace Application.Finance.Bills.Shared;

public sealed record NewVehicleInput(
  string RegistrationNumber,
  Guid ServiceId);
