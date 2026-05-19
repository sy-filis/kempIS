using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.SpotGroups;
using Domain.Services.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.SpotGroups;

public sealed record SpotGroupResponse(
  Guid Id,
  Guid ServiceId,
  string Name,
  string? Description,
  uint Capacity,
  bool IsActive,
  string ImageUrl,
  string DetailsUrl);

public sealed record GetSpotGroupsQuery : IQuery<List<SpotGroupResponse>>;

internal sealed class GetSpotGroupsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetSpotGroupsQuery, List<SpotGroupResponse>>
{
  public async Task<Result<List<SpotGroupResponse>>> Handle(
    GetSpotGroupsQuery query,
    CancellationToken cancellationToken)
  {
    List<SpotGroupResponse> spotGroups = await context.SpotGroups
      .Select(sg => new SpotGroupResponse(
        sg.Id,
        sg.ServiceId,
        sg.Name,
        sg.Description,
        sg.Capacity,
        sg.IsActive,
        sg.ImageUrl,
        sg.DetailsUrl))
      .ToListAsync(cancellationToken);

    return spotGroups;
  }
}

public sealed record CreateSpotGroupCommand(
  Guid ServiceId,
  string Name,
  string? Description,
  uint Capacity,
  bool IsActive,
  string ImageUrl,
  string DetailsUrl) : ICommand<Guid>;

internal sealed class CreateSpotGroupCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateSpotGroupCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateSpotGroupCommand command,
    CancellationToken cancellationToken)
  {
    Service? service = await context.Services
      .FirstOrDefaultAsync(s => s.Id == command.ServiceId, cancellationToken);

    if (service is null)
    {
      return Result.Failure<Guid>(ServiceErrors.NotFound(command.ServiceId));
    }

    if (service.ServiceGroup != ServiceGroup.Spots)
    {
      return Result.Failure<Guid>(SpotGroupErrors.ServiceNotInSpotsGroup(command.ServiceId));
    }

    SpotGroup spotGroup = new()
    {
      Id = Guid.NewGuid(),
      ServiceId = command.ServiceId,
      Name = command.Name,
      Description = command.Description,
      Capacity = command.Capacity,
      IsActive = command.IsActive,
      ImageUrl = command.ImageUrl,
      DetailsUrl = command.DetailsUrl
    };

    context.SpotGroups.Add(spotGroup);
    await context.SaveChangesAsync(cancellationToken);

    return spotGroup.Id;
  }
}

internal sealed class CreateSpotGroupCommandValidator : AbstractValidator<CreateSpotGroupCommand>
{
  public CreateSpotGroupCommandValidator()
  {
    RuleFor(c => c.ServiceId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);

    RuleFor(c => c.Capacity)
      .GreaterThan(0u);

    RuleFor(c => c.ImageUrl)
      .NotEmpty()
      .MaximumLength(2048);

    RuleFor(c => c.DetailsUrl)
      .NotEmpty()
      .MaximumLength(2048);
  }
}

public sealed record UpdateSpotGroupCommand(
  Guid Id,
  Guid ServiceId,
  string Name,
  string? Description,
  uint Capacity,
  bool IsActive,
  string ImageUrl,
  string DetailsUrl) : ICommand;

internal sealed class UpdateSpotGroupCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateSpotGroupCommand>
{
  public async Task<Result> Handle(
    UpdateSpotGroupCommand command,
    CancellationToken cancellationToken)
  {
    SpotGroup? spotGroup = await context.SpotGroups
      .FirstOrDefaultAsync(sg => sg.Id == command.Id, cancellationToken);

    if (spotGroup is null)
    {
      return Result.Failure(SpotGroupErrors.NotFound(command.Id));
    }

    Service? service = await context.Services
      .FirstOrDefaultAsync(s => s.Id == command.ServiceId, cancellationToken);

    if (service is null)
    {
      return Result.Failure(ServiceErrors.NotFound(command.ServiceId));
    }

    if (service.ServiceGroup != ServiceGroup.Spots)
    {
      return Result.Failure(SpotGroupErrors.ServiceNotInSpotsGroup(command.ServiceId));
    }

    spotGroup.ServiceId = command.ServiceId;
    spotGroup.Name = command.Name;
    spotGroup.Description = command.Description;
    spotGroup.Capacity = command.Capacity;
    spotGroup.IsActive = command.IsActive;
    spotGroup.ImageUrl = command.ImageUrl;
    spotGroup.DetailsUrl = command.DetailsUrl;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateSpotGroupCommandValidator : AbstractValidator<UpdateSpotGroupCommand>
{
  public UpdateSpotGroupCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.ServiceId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);

    RuleFor(c => c.Capacity)
      .GreaterThan(0u);

    RuleFor(c => c.ImageUrl)
      .NotEmpty()
      .MaximumLength(2048);

    RuleFor(c => c.DetailsUrl)
      .NotEmpty()
      .MaximumLength(2048);
  }
}

public sealed record DeleteSpotGroupCommand(Guid Id) : ICommand;

internal sealed class DeleteSpotGroupCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteSpotGroupCommand>
{
  public async Task<Result> Handle(
    DeleteSpotGroupCommand command,
    CancellationToken cancellationToken)
  {
    SpotGroup? spotGroup = await context.SpotGroups
      .FirstOrDefaultAsync(sg => sg.Id == command.Id, cancellationToken);

    if (spotGroup is null)
    {
      return Result.Failure(SpotGroupErrors.NotFound(command.Id));
    }

    context.SpotGroups.Remove(spotGroup);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteSpotGroupCommandValidator : AbstractValidator<DeleteSpotGroupCommand>
{
  public DeleteSpotGroupCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
