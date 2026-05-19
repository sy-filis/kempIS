using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Operations.CleanInfos;
using Domain.Operations.CleaningPlans;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Operations.CleaningPlans;

public sealed record CleanInfoResponse(
  Guid Id,
  Guid SpotId,
  DateTime? CompletedAtUtc,
  Guid? ResponsibleUserId,
  string? Note);

public sealed record CleaningPlanDetailResponse(
  Guid Id,
  DateOnly Date,
  DateTime? UpdatedAtUtc,
  Guid? UpdatedByUserId,
  IReadOnlyList<CleanInfoResponse> CleanInfos);

public sealed record GetCleaningPlanByDateQuery(DateOnly Date) : IQuery<CleaningPlanDetailResponse>;

internal sealed class GetCleaningPlanByDateQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetCleaningPlanByDateQuery, CleaningPlanDetailResponse>
{
  public async Task<Result<CleaningPlanDetailResponse>> Handle(
    GetCleaningPlanByDateQuery query,
    CancellationToken cancellationToken)
  {
    Guid planId = await CleaningPlanProvisioning.EnsurePlanExistsForDateAsync(
      context, query.Date, cancellationToken);

    CleaningPlanDetailResponse? plan = await context.CleaningPlans
      .AsNoTracking()
      .Where(p => p.Id == planId)
      .Select(p => new CleaningPlanDetailResponse(
        p.Id,
        p.Date,
        p.UpdatedAtUtc,
        p.UpdatedByUserId,
        context.CleanInfos
          .Where(ci => ci.CleaningPlanId == p.Id)
          .Select(ci => new CleanInfoResponse(
            ci.Id,
            ci.SpotId,
            ci.CompletedAtUtc,
            ci.ResponsibleUserId,
            ci.Note))
          .ToList()))
      .FirstOrDefaultAsync(cancellationToken);

    return plan ?? throw new InvalidOperationException(
      "Cleaning plan disappeared between EnsurePlanExistsForDateAsync and the projection query. " +
      "This should be impossible since the helper persists before returning.");
  }
}

public sealed record AddCleanInfoCommand(DateOnly Date, Guid SpotId) : ICommand<Guid>;

internal sealed class AddCleanInfoCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IUserContext userContext)
  : ICommandHandler<AddCleanInfoCommand, Guid>
{
  public async Task<Result<Guid>> Handle(AddCleanInfoCommand command, CancellationToken cancellationToken)
  {
    Guid planId = await CleaningPlanProvisioning.EnsurePlanExistsForDateAsync(
      context, command.Date, cancellationToken);

    bool duplicate = await context.CleanInfos
      .AnyAsync(ci => ci.CleaningPlanId == planId && ci.SpotId == command.SpotId, cancellationToken);
    if (duplicate)
    {
      return Result.Failure<Guid>(CleaningPlanErrors.SpotAlreadyInPlan(command.SpotId));
    }

    CleanInfo cleanInfo = new()
    {
      Id = Guid.NewGuid(),
      CleaningPlanId = planId,
      SpotId = command.SpotId
    };
    context.CleanInfos.Add(cleanInfo);
    await CleaningPlanProvisioning.StampPlanChangeAsync(
      context, planId, dateTimeProvider.UtcNow, userContext.UserId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return cleanInfo.Id;
  }
}

public sealed record DeleteCleanInfoCommand(Guid Id) : ICommand;

internal sealed class DeleteCleanInfoCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IUserContext userContext)
  : ICommandHandler<DeleteCleanInfoCommand>
{
  public async Task<Result> Handle(DeleteCleanInfoCommand command, CancellationToken cancellationToken)
  {
    CleanInfo? cleanInfo = await context.CleanInfos.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
    if (cleanInfo is null)
    {
      return Result.Failure(CleanInfoErrors.NotFound(command.Id));
    }
    context.CleanInfos.Remove(cleanInfo);
    await CleaningPlanProvisioning.StampPlanChangeAsync(
      context, cleanInfo.CleaningPlanId, dateTimeProvider.UtcNow, userContext.UserId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

public sealed record UpdateCleanInfoCommand(Guid Id, string? Note) : ICommand;

internal sealed class UpdateCleanInfoCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IUserContext userContext)
  : ICommandHandler<UpdateCleanInfoCommand>
{
  public async Task<Result> Handle(UpdateCleanInfoCommand command, CancellationToken cancellationToken)
  {
    CleanInfo? cleanInfo = await context.CleanInfos.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
    if (cleanInfo is null)
    {
      return Result.Failure(CleanInfoErrors.NotFound(command.Id));
    }
    if (command.Note is not null)
    {
      cleanInfo.Note = command.Note;
    }
    await CleaningPlanProvisioning.StampPlanChangeAsync(
      context, cleanInfo.CleaningPlanId, dateTimeProvider.UtcNow, userContext.UserId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

public sealed record MarkCleanInfoCleanedCommand(Guid Id, string? Note) : ICommand;

internal sealed class MarkCleanInfoCleanedCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IUserContext userContext)
  : ICommandHandler<MarkCleanInfoCleanedCommand>
{
  public async Task<Result> Handle(MarkCleanInfoCleanedCommand command, CancellationToken cancellationToken)
  {
    CleanInfo? cleanInfo = await context.CleanInfos.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
    if (cleanInfo is null)
    {
      return Result.Failure(CleanInfoErrors.NotFound(command.Id));
    }
    if (cleanInfo.CompletedAtUtc is not null)
    {
      return Result.Failure(CleanInfoErrors.AlreadyCompleted(command.Id));
    }

    DateTime now = dateTimeProvider.UtcNow;
    Guid actorUserId = userContext.UserId;

    cleanInfo.CompletedAtUtc = now;
    cleanInfo.ResponsibleUserId = actorUserId;
    if (command.Note is not null)
    {
      cleanInfo.Note = command.Note;
    }

    await CleaningPlanProvisioning.StampPlanChangeAsync(
      context, cleanInfo.CleaningPlanId, now, actorUserId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

internal static class CleaningPlanProvisioning
{
  public static async Task<Guid> EnsurePlanExistsForDateAsync(
    IApplicationDbContext context,
    DateOnly date,
    CancellationToken cancellationToken)
  {
    Guid? existingId = await context.CleaningPlans
      .Where(p => p.Date == date)
      .Select(p => (Guid?)p.Id)
      .FirstOrDefaultAsync(cancellationToken);
    if (existingId is not null)
    {
      return existingId.Value;
    }

    CleaningPlan plan = new()
    {
      Id = Guid.NewGuid(),
      Date = date,
      UpdatedAtUtc = null,
      UpdatedByUserId = null,
    };
    context.CleaningPlans.Add(plan);

    await context.SaveChangesAsync(cancellationToken);
    return plan.Id;
  }

  public static async Task StampPlanChangeAsync(
    IApplicationDbContext context,
    Guid planId,
    DateTime nowUtc,
    Guid actorUserId,
    CancellationToken cancellationToken)
  {
    CleaningPlan? plan = await context.CleaningPlans
      .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    if (plan is null)
    {
      return;
    }
    plan.UpdatedAtUtc = nowUtc;
    plan.UpdatedByUserId = actorUserId;
  }
}
