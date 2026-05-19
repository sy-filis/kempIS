using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Services;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Services.Services;

public sealed record ServiceResponse(
  Guid Id,
  ServiceGroup ServiceGroup,
  Guid ServiceTypeId,
  Guid VatRateId,
  string Name,
  decimal BasePrice,
  bool IsActive);

public sealed record GetServicesQuery : IQuery<List<ServiceResponse>>;

internal sealed class GetServicesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetServicesQuery, List<ServiceResponse>>
{
  public async Task<Result<List<ServiceResponse>>> Handle(
    GetServicesQuery query,
    CancellationToken cancellationToken)
  {
    List<ServiceResponse> services = await context.Services
      .Select(s => new ServiceResponse(
        s.Id,
        s.ServiceGroup,
        s.ServiceTypeId,
        s.VatRateId,
        s.Name,
        s.BasePrice,
        s.IsActive))
      .ToListAsync(cancellationToken);

    return services;
  }
}

public sealed record CreateServiceCommand(
  ServiceGroup ServiceGroup,
  Guid ServiceTypeId,
  Guid VatRateId,
  string Name,
  decimal BasePrice,
  bool IsActive) : ICommand<Guid>;

internal sealed class CreateServiceCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateServiceCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateServiceCommand command,
    CancellationToken cancellationToken)
  {
    if (!await context.ServiceTypes.AnyAsync(st => st.Id == command.ServiceTypeId, cancellationToken))
    {
      return Result.Failure<Guid>(ServiceTypeErrors.NotFound(command.ServiceTypeId));
    }

    if (!await context.VatRates.AnyAsync(vr => vr.Id == command.VatRateId, cancellationToken))
    {
      return Result.Failure<Guid>(VatRateErrors.NotFound(command.VatRateId));
    }

    Service service = new()
    {
      Id = Guid.NewGuid(),
      ServiceGroup = command.ServiceGroup,
      ServiceTypeId = command.ServiceTypeId,
      VatRateId = command.VatRateId,
      Name = command.Name,
      BasePrice = command.BasePrice,
      IsActive = command.IsActive
    };

    context.Services.Add(service);
    await context.SaveChangesAsync(cancellationToken);

    return service.Id;
  }
}

internal sealed class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
  public CreateServiceCommandValidator()
  {
    RuleFor(c => c.ServiceGroup)
      .IsInEnum();

    RuleFor(c => c.ServiceTypeId)
      .NotEmpty();

    RuleFor(c => c.VatRateId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.BasePrice)
      .GreaterThanOrEqualTo(0);
  }
}

public sealed record UpdateServiceCommand(
  Guid Id,
  ServiceGroup ServiceGroup,
  Guid ServiceTypeId,
  Guid VatRateId,
  string Name,
  decimal BasePrice,
  bool IsActive) : ICommand;

internal sealed class UpdateServiceCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateServiceCommand>
{
  public async Task<Result> Handle(
    UpdateServiceCommand command,
    CancellationToken cancellationToken)
  {
    Service? service = await context.Services
      .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

    if (service is null)
    {
      return Result.Failure(ServiceErrors.NotFound(command.Id));
    }

    if (!await context.ServiceTypes.AnyAsync(st => st.Id == command.ServiceTypeId, cancellationToken))
    {
      return Result.Failure(ServiceTypeErrors.NotFound(command.ServiceTypeId));
    }

    if (!await context.VatRates.AnyAsync(vr => vr.Id == command.VatRateId, cancellationToken))
    {
      return Result.Failure(VatRateErrors.NotFound(command.VatRateId));
    }

    service.ServiceGroup = command.ServiceGroup;
    service.ServiceTypeId = command.ServiceTypeId;
    service.VatRateId = command.VatRateId;
    service.Name = command.Name;
    service.BasePrice = command.BasePrice;
    service.IsActive = command.IsActive;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateServiceCommandValidator : AbstractValidator<UpdateServiceCommand>
{
  public UpdateServiceCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.ServiceGroup)
      .IsInEnum();

    RuleFor(c => c.ServiceTypeId)
      .NotEmpty();

    RuleFor(c => c.VatRateId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.BasePrice)
      .GreaterThanOrEqualTo(0);
  }
}

public sealed record DeleteServiceCommand(Guid Id) : ICommand;

internal sealed class DeleteServiceCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteServiceCommand>
{
  public async Task<Result> Handle(
    DeleteServiceCommand command,
    CancellationToken cancellationToken)
  {
    Service? service = await context.Services
      .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

    if (service is null)
    {
      return Result.Failure(ServiceErrors.NotFound(command.Id));
    }

    context.Services.Remove(service);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteServiceCommandValidator : AbstractValidator<DeleteServiceCommand>
{
  public DeleteServiceCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
