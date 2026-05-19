using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Vehicles;

public sealed record VehicleResponse(
  Guid Id,
  Guid? ReservationId,
  Guid? BillId,
  Guid? ServiceId,
  string RegistrationNumber);

public sealed record GetVehiclesQuery(DateOnly From, DateOnly To, string? Search)
  : IQuery<List<VehicleResponse>>;

internal sealed class GetVehiclesQueryValidator : AbstractValidator<GetVehiclesQuery>
{
  public GetVehiclesQueryValidator()
  {
    RuleFor(q => q.From).NotEmpty();
    RuleFor(q => q.To).NotEmpty().GreaterThanOrEqualTo(q => q.From);
    RuleFor(q => q.Search).MaximumLength(100).When(q => q.Search is not null);
  }
}

internal sealed class GetVehiclesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetVehiclesQuery, List<VehicleResponse>>
{
  public async Task<Result<List<VehicleResponse>>> Handle(
    GetVehiclesQuery query,
    CancellationToken cancellationToken)
  {
    IQueryable<Vehicle> queryable = context.Vehicles
      .AsNoTracking()
      .Where(v => v.BillId != null
               && context.Bills.Any(b => b.Id == v.BillId
                                      && b.CheckInAt <= query.To
                                      && b.CheckOutAt >= query.From));

    string? trimmed = query.Search?.Trim();
    if (!string.IsNullOrEmpty(trimmed))
    {
#pragma warning disable CA1304, CA1311, CA1862 // EF translates LOWER(col) LIKE '%p%' on both Npgsql and SQLite; StringComparison overloads are not supported in expression trees
      string pattern = trimmed.ToLower();
      queryable = queryable.Where(v => v.RegistrationNumber.ToLower().Contains(pattern));
#pragma warning restore CA1304, CA1311, CA1862
    }

    List<VehicleResponse> vehicles = await queryable
      .Select(v => new VehicleResponse(
        v.Id,
        v.ReservationId,
        v.BillId,
        v.ServiceId,
        v.RegistrationNumber))
      .ToListAsync(cancellationToken);

    return vehicles;
  }
}

public sealed record CreateVehicleCommand(
  Guid? ReservationId,
  Guid? BillId,
  Guid? ServiceId,
  string RegistrationNumber) : ICommand<Guid>;

internal sealed class CreateVehicleCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateVehicleCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateVehicleCommand command,
    CancellationToken cancellationToken)
  {
    Vehicle vehicle = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = command.ReservationId,
      BillId = command.BillId,
      ServiceId = command.ServiceId,
      RegistrationNumber = command.RegistrationNumber
    };

    context.Vehicles.Add(vehicle);
    await context.SaveChangesAsync(cancellationToken);

    return vehicle.Id;
  }
}

internal sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
  public CreateVehicleCommandValidator()
  {
    RuleFor(c => c.ReservationId)
      .NotEqual(Guid.Empty)
      .When(c => c.ReservationId is not null);

    RuleFor(c => c.BillId)
      .NotEqual(Guid.Empty)
      .When(c => c.BillId is not null);

    RuleFor(c => c.ServiceId)
      .NotEqual(Guid.Empty)
      .When(c => c.ServiceId is not null);

    RuleFor(c => c.RegistrationNumber)
      .NotEmpty()
      .MaximumLength(20);
  }
}

public sealed record UpdateVehicleCommand(
  Guid Id,
  Guid? ReservationId,
  Guid? BillId,
  Guid? ServiceId,
  string RegistrationNumber) : ICommand;

internal sealed class UpdateVehicleCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateVehicleCommand>
{
  public async Task<Result> Handle(
    UpdateVehicleCommand command,
    CancellationToken cancellationToken)
  {
    Vehicle? vehicle = await context.Vehicles
      .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken);

    if (vehicle is null)
    {
      return Result.Failure(VehicleErrors.NotFound(command.Id));
    }

    vehicle.ReservationId = command.ReservationId;
    vehicle.BillId = command.BillId;
    vehicle.ServiceId = command.ServiceId;
    vehicle.RegistrationNumber = command.RegistrationNumber;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
  public UpdateVehicleCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();

    RuleFor(c => c.ReservationId)
      .NotEqual(Guid.Empty)
      .When(c => c.ReservationId is not null);

    RuleFor(c => c.BillId)
      .NotEqual(Guid.Empty)
      .When(c => c.BillId is not null);

    RuleFor(c => c.ServiceId)
      .NotEqual(Guid.Empty)
      .When(c => c.ServiceId is not null);

    RuleFor(c => c.RegistrationNumber)
      .NotEmpty()
      .MaximumLength(20);
  }
}

public sealed record DeleteVehicleCommand(Guid Id) : ICommand;

internal sealed class DeleteVehicleCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteVehicleCommand>
{
  public async Task<Result> Handle(
    DeleteVehicleCommand command,
    CancellationToken cancellationToken)
  {
    Vehicle? vehicle = await context.Vehicles
      .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken);

    if (vehicle is null)
    {
      return Result.Failure(VehicleErrors.NotFound(command.Id));
    }

    context.Vehicles.Remove(vehicle);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteVehicleCommandValidator : AbstractValidator<DeleteVehicleCommand>
{
  public DeleteVehicleCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}

public sealed record VehicleLookupResponse(string LicencePlate, DateOnly CheckoutAt);

public sealed record GetVehicleByPlateQuery(string LicencePlate) : IQuery<VehicleLookupResponse>;

internal static class VehicleLookupErrors
{
  public static readonly Error EmptyPlate = Error.Problem(
    "Vehicles.Lookup.EmptyPlate",
    "Licence plate must contain at least one alphanumeric character.");

  public static readonly Error PlateTooLong = Error.Problem(
    "Vehicles.Lookup.PlateTooLong",
    "Licence plate must be at most 40 characters.");

  public static readonly Error NotFound = Error.NotFound(
    "Vehicles.Lookup.NotFound",
    "No vehicle with the supplied licence plate is currently associated with an open bill.");
}

internal sealed class GetVehicleByPlateQueryHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<GetVehicleByPlateQuery, VehicleLookupResponse>
{
  private const int MaxPlateLength = 40;

  public async Task<Result<VehicleLookupResponse>> Handle(
    GetVehicleByPlateQuery query,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(query.LicencePlate))
    {
      return Result.Failure<VehicleLookupResponse>(VehicleLookupErrors.EmptyPlate);
    }

    if (query.LicencePlate.Length > MaxPlateLength)
    {
      return Result.Failure<VehicleLookupResponse>(VehicleLookupErrors.PlateTooLong);
    }

    string normalizedInput = LicencePlateNormalizer.Normalize(query.LicencePlate);
    if (normalizedInput.Length == 0)
    {
      return Result.Failure<VehicleLookupResponse>(VehicleLookupErrors.EmptyPlate);
    }

    var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.Date);

    var candidates = await context.Vehicles
      .AsNoTracking()
      .Where(v => v.BillId != null)
      .Join(
        context.Bills.AsNoTracking(),
        v => v.BillId,
        b => b.Id,
        (v, b) => new { v.RegistrationNumber, b.CheckOutAt })
      .Where(x => x.CheckOutAt >= today)
      .ToListAsync(cancellationToken);

    var match = candidates
      .Select(c => new
      {
        Normalized = LicencePlateNormalizer.Normalize(c.RegistrationNumber),
        c.CheckOutAt,
      })
      .Where(c => c.Normalized == normalizedInput)
      .OrderByDescending(c => c.CheckOutAt)
      .FirstOrDefault();

    return match is null
      ? Result.Failure<VehicleLookupResponse>(VehicleLookupErrors.NotFound)
      : Result.Success(new VehicleLookupResponse(match.Normalized, match.CheckOutAt));
  }
}
