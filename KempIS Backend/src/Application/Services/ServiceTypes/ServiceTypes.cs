using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Services.ServiceTypes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Services.ServiceTypes;

public sealed record ServiceTypeResponse(Guid Id, string Name, bool IsActive);

public sealed record GetServiceTypesQuery : IQuery<List<ServiceTypeResponse>>;

internal sealed class GetServiceTypesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetServiceTypesQuery, List<ServiceTypeResponse>>
{
  public async Task<Result<List<ServiceTypeResponse>>> Handle(
    GetServiceTypesQuery query,
    CancellationToken cancellationToken)
  {
    List<ServiceTypeResponse> serviceTypes = await context.ServiceTypes
      .Select(st => new ServiceTypeResponse(st.Id, st.Name, st.IsActive))
      .ToListAsync(cancellationToken);

    return serviceTypes;
  }
}

public sealed record CreateServiceTypeCommand(string Name, bool IsActive) : ICommand<Guid>;

internal sealed class CreateServiceTypeCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateServiceTypeCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateServiceTypeCommand command,
    CancellationToken cancellationToken)
  {
    ServiceType serviceType = new()
    {
      Id = Guid.NewGuid(),
      Name = command.Name,
      IsActive = command.IsActive
    };

    context.ServiceTypes.Add(serviceType);
    await context.SaveChangesAsync(cancellationToken);

    return serviceType.Id;
  }
}

internal sealed class CreateServiceTypeCommandValidator : AbstractValidator<CreateServiceTypeCommand>
{
  public CreateServiceTypeCommandValidator()
  {
    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);
  }
}

public sealed record UpdateServiceTypeCommand(Guid Id, string Name, bool IsActive) : ICommand;

internal sealed class UpdateServiceTypeCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateServiceTypeCommand>
{
  public async Task<Result> Handle(
    UpdateServiceTypeCommand command,
    CancellationToken cancellationToken)
  {
    ServiceType? serviceType = await context.ServiceTypes
      .FirstOrDefaultAsync(st => st.Id == command.Id, cancellationToken);

    if (serviceType is null)
    {
      return Result.Failure(ServiceTypeErrors.NotFound(command.Id));
    }

    serviceType.Name = command.Name;
    serviceType.IsActive = command.IsActive;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateServiceTypeCommandValidator : AbstractValidator<UpdateServiceTypeCommand>
{
  public UpdateServiceTypeCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);
  }
}

public sealed record DeleteServiceTypeCommand(Guid Id) : ICommand;

internal sealed class DeleteServiceTypeCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteServiceTypeCommand>
{
  public async Task<Result> Handle(
    DeleteServiceTypeCommand command,
    CancellationToken cancellationToken)
  {
    ServiceType? serviceType = await context.ServiceTypes
      .FirstOrDefaultAsync(st => st.Id == command.Id, cancellationToken);

    if (serviceType is null)
    {
      return Result.Failure(ServiceTypeErrors.NotFound(command.Id));
    }

    context.ServiceTypes.Remove(serviceType);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteServiceTypeCommandValidator : AbstractValidator<DeleteServiceTypeCommand>
{
  public DeleteServiceTypeCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
