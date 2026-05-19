using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Operations.OutOfOrders;

public sealed record OutOfOrderResponse(
  Guid Id,
  DateOnly From,
  DateOnly To,
  string Reason,
  List<Guid> SpotGroupIds,
  List<Guid> SpotIds);

public sealed record GetOutOfOrdersQuery(DateOnly? From, DateOnly? To) : IQuery<List<OutOfOrderResponse>>;

internal sealed class GetOutOfOrdersQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetOutOfOrdersQuery, List<OutOfOrderResponse>>
{
  public async Task<Result<List<OutOfOrderResponse>>> Handle(
    GetOutOfOrdersQuery query,
    CancellationToken cancellationToken)
  {
    IQueryable<OutOfOrder> source = context.OutOfOrders;

    if (query.From is { } from)
    {
      source = source.Where(o => o.Period.To >= from);
    }

    if (query.To is { } to)
    {
      source = source.Where(o => o.Period.From <= to);
    }

    List<OutOfOrderResponse> outOfOrders = await source
      .Select(o => new OutOfOrderResponse(
        o.Id,
        o.Period.From,
        o.Period.To,
        o.Reason,
        o.SpotGroupItems.Select(sg => sg.SpotGroupId).ToList(),
        o.SpotItems.Select(s => s.SpotId).ToList()))
      .ToListAsync(cancellationToken);

    return outOfOrders;
  }
}

public sealed record CreateOutOfOrderCommand(
  DateOnly From,
  DateOnly To,
  string Reason,
  IReadOnlyList<Guid> SpotGroupIds,
  IReadOnlyList<Guid> SpotIds) : ICommand<Guid>;

internal sealed class CreateOutOfOrderCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateOutOfOrderCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateOutOfOrderCommand command,
    CancellationToken cancellationToken)
  {
    var spotGroupIds = command.SpotGroupIds.Distinct().ToList();
    var spotIds = command.SpotIds.Distinct().ToList();

    var outOfOrderId = Guid.NewGuid();

    var outOfOrder = new OutOfOrder
    {
      Id = outOfOrderId,
      Period = new DateRange(command.From, command.To),
      Reason = command.Reason,
      SpotGroupItems = spotGroupIds
        .Select(id => new SpotGroupOofItem
        {
          Id = Guid.NewGuid(),
          OutOfOrderId = outOfOrderId,
          SpotGroupId = id
        })
        .ToList(),
      SpotItems = spotIds
        .Select(id => new SpotOofItem
        {
          Id = Guid.NewGuid(),
          OutOfOrderId = outOfOrderId,
          SpotId = id
        })
        .ToList()
    };

    context.OutOfOrders.Add(outOfOrder);
    await context.SaveChangesAsync(cancellationToken);

    return outOfOrder.Id;
  }
}

internal sealed class CreateOutOfOrderCommandValidator : AbstractValidator<CreateOutOfOrderCommand>
{
  public CreateOutOfOrderCommandValidator()
  {
    RuleFor(c => c.From)
      .GreaterThan(DateOnly.MinValue);

    RuleFor(c => c.To)
      .GreaterThanOrEqualTo(c => c.From);

    RuleFor(c => c.Reason)
      .NotEmpty()
      .MaximumLength(1000);

    RuleFor(c => c)
      .Must(c => (c.SpotGroupIds?.Count ?? 0) > 0 || (c.SpotIds?.Count ?? 0) > 0)
      .WithMessage("At least one spot group or spot must be provided.");

    RuleFor(c => c.SpotGroupIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotGroupIds must be unique.")
      .When(c => c.SpotGroupIds is not null);

    RuleFor(c => c.SpotIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotIds must be unique.")
      .When(c => c.SpotIds is not null);
  }
}

public sealed record UpdateOutOfOrderCommand(
  Guid Id,
  DateOnly From,
  DateOnly To,
  string Reason,
  IReadOnlyList<Guid> SpotGroupIds,
  IReadOnlyList<Guid> SpotIds) : ICommand;

internal sealed class UpdateOutOfOrderCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateOutOfOrderCommand>
{
  public async Task<Result> Handle(
    UpdateOutOfOrderCommand command,
    CancellationToken cancellationToken)
  {
    OutOfOrder? outOfOrder = await context.OutOfOrders
      .Include(o => o.SpotGroupItems)
      .Include(o => o.SpotItems)
      .FirstOrDefaultAsync(o => o.Id == command.Id, cancellationToken);

    if (outOfOrder is null)
    {
      return Result.Failure(OutOfOrderErrors.NotFound(command.Id));
    }

    outOfOrder.Period = new DateRange(command.From, command.To);
    outOfOrder.Reason = command.Reason;

    foreach (SpotGroupOofItem existing in outOfOrder.SpotGroupItems.ToList())
    {
      context.SpotGroupOofItems.Remove(existing);
    }

    foreach (SpotOofItem existing in outOfOrder.SpotItems.ToList())
    {
      context.SpotOofItems.Remove(existing);
    }

    foreach (Guid id in command.SpotGroupIds.Distinct())
    {
      context.SpotGroupOofItems.Add(new SpotGroupOofItem
      {
        Id = Guid.NewGuid(),
        OutOfOrderId = outOfOrder.Id,
        SpotGroupId = id
      });
    }

    foreach (Guid id in command.SpotIds.Distinct())
    {
      context.SpotOofItems.Add(new SpotOofItem
      {
        Id = Guid.NewGuid(),
        OutOfOrderId = outOfOrder.Id,
        SpotId = id
      });
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateOutOfOrderCommandValidator : AbstractValidator<UpdateOutOfOrderCommand>
{
  public UpdateOutOfOrderCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.From)
      .GreaterThan(DateOnly.MinValue);

    RuleFor(c => c.To)
      .GreaterThanOrEqualTo(c => c.From);

    RuleFor(c => c.Reason)
      .NotEmpty()
      .MaximumLength(1000);

    RuleFor(c => c)
      .Must(c => (c.SpotGroupIds?.Count ?? 0) > 0 || (c.SpotIds?.Count ?? 0) > 0)
      .WithMessage("At least one spot group or spot must be provided.");

    RuleFor(c => c.SpotGroupIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotGroupIds must be unique.")
      .When(c => c.SpotGroupIds is not null);

    RuleFor(c => c.SpotIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithMessage("SpotIds must be unique.")
      .When(c => c.SpotIds is not null);
  }
}

public sealed record DeleteOutOfOrderCommand(Guid Id) : ICommand;

internal sealed class DeleteOutOfOrderCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteOutOfOrderCommand>
{
  public async Task<Result> Handle(
    DeleteOutOfOrderCommand command,
    CancellationToken cancellationToken)
  {
    OutOfOrder? outOfOrder = await context.OutOfOrders
      .FirstOrDefaultAsync(o => o.Id == command.Id, cancellationToken);

    if (outOfOrder is null)
    {
      return Result.Failure(OutOfOrderErrors.NotFound(command.Id));
    }

    context.OutOfOrders.Remove(outOfOrder);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteOutOfOrderCommandValidator : AbstractValidator<DeleteOutOfOrderCommand>
{
  public DeleteOutOfOrderCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
