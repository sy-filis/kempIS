using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Operations.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Operations.Events;

public sealed record EventResponse(
  Guid Id,
  string Name,
  string? Description,
  DateOnly StartsAt,
  DateOnly? EndsAt,
  List<Guid> SpotGroupIds);

public sealed record GetEventsQuery : IQuery<List<EventResponse>>;

internal sealed class GetEventsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetEventsQuery, List<EventResponse>>
{
  public async Task<Result<List<EventResponse>>> Handle(
    GetEventsQuery query,
    CancellationToken cancellationToken)
  {
    List<EventResponse> events = await context.Events
      .Select(e => new EventResponse(
        e.Id,
        e.Name,
        e.Description,
        e.StartsAt,
        e.EndsAt,
        e.SpotGroupItems
          .Select(sg => sg.SpotGroupId)
          .ToList()))
      .ToListAsync(cancellationToken);

    return events;
  }
}

public sealed record CreateEventCommand(
  string Name,
  string? Description,
  DateOnly StartsAt,
  DateOnly? EndsAt,
  IReadOnlyList<Guid> SpotGroupIds) : ICommand<Guid>;

internal sealed class CreateEventCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateEventCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateEventCommand command,
    CancellationToken cancellationToken)
  {
    var spotGroupIds = command.SpotGroupIds.Distinct().ToList();

    var eventId = Guid.NewGuid();

    var @event = new Event
    {
      Id = eventId,
      Name = command.Name,
      Description = command.Description,
      StartsAt = command.StartsAt,
      EndsAt = command.EndsAt,
      SpotGroupItems = spotGroupIds
        .Select(id => new EventSpotGroupItem
        {
          Id = Guid.NewGuid(),
          EventId = eventId,
          SpotGroupId = id
        })
        .ToList()
    };

    context.Events.Add(@event);
    await context.SaveChangesAsync(cancellationToken);

    return @event.Id;
  }
}

internal sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
  public CreateEventCommandValidator()
  {
    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);

    RuleFor(c => c.StartsAt)
      .NotEmpty();

    RuleFor(c => c.EndsAt)
      .Must((command, endsAt) => !endsAt.HasValue || endsAt.Value >= command.StartsAt)
      .WithMessage("'EndsAt' must be on or after 'StartsAt'.");

    RuleFor(c => c.SpotGroupIds)
      .NotEmpty()
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotGroupIds must be unique.");
  }
}

public sealed record UpdateEventCommand(
  Guid Id,
  string Name,
  string? Description,
  DateOnly StartsAt,
  DateOnly? EndsAt,
  IReadOnlyList<Guid> SpotGroupIds) : ICommand;

internal sealed class UpdateEventCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateEventCommand>
{
  public async Task<Result> Handle(
    UpdateEventCommand command,
    CancellationToken cancellationToken)
  {
    Event? @event = await context.Events
      .Include(e => e.SpotGroupItems)
      .FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);

    if (@event is null)
    {
      return Result.Failure(EventErrors.NotFound(command.Id));
    }

    @event.Name = command.Name;
    @event.Description = command.Description;
    @event.StartsAt = command.StartsAt;
    @event.EndsAt = command.EndsAt;

    foreach (EventSpotGroupItem existing in @event.SpotGroupItems.ToList())
    {
      context.EventSpotGroupItems.Remove(existing);
    }

    foreach (Guid id in command.SpotGroupIds.Distinct())
    {
      context.EventSpotGroupItems.Add(new EventSpotGroupItem
      {
        Id = Guid.NewGuid(),
        EventId = @event.Id,
        SpotGroupId = id
      });
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
  public UpdateEventCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);

    RuleFor(c => c.StartsAt)
      .NotEmpty();

    RuleFor(c => c.EndsAt)
      .Must((command, endsAt) => !endsAt.HasValue || endsAt.Value >= command.StartsAt)
      .WithMessage("'EndsAt' must be on or after 'StartsAt'.");

    RuleFor(c => c.SpotGroupIds)
      .NotEmpty()
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotGroupIds must be unique.");
  }
}

public sealed record DeleteEventCommand(Guid Id) : ICommand;

internal sealed class DeleteEventCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteEventCommand>
{
  public async Task<Result> Handle(
    DeleteEventCommand command,
    CancellationToken cancellationToken)
  {
    Event? @event = await context.Events
      .FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);

    if (@event is null)
    {
      return Result.Failure(EventErrors.NotFound(command.Id));
    }

    context.Events.Remove(@event);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteEventCommandValidator : AbstractValidator<DeleteEventCommand>
{
  public DeleteEventCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
