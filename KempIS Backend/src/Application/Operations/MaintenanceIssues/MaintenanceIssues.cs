using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Operations.MaintenanceIssues;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Operations.MaintenanceIssues;

public enum MaintenanceIssueStatus { Open, Resolved, All }

public sealed record MaintenanceIssueResponse(
  Guid Id,
  Guid? SpotId,
  DateTime IssuedAtUtc,
  string ProblemDescription,
  Guid? SolverUserId,
  DateTime? ResolvedAtUtc,
  string? Note);

public sealed record GetMaintenanceIssuesQuery(
  MaintenanceIssueStatus Status,
  Guid? SpotId,
  DateTime? From,
  DateTime? To)
  : IQuery<IReadOnlyList<MaintenanceIssueResponse>>;

internal sealed class GetMaintenanceIssuesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetMaintenanceIssuesQuery, IReadOnlyList<MaintenanceIssueResponse>>
{
  public async Task<Result<IReadOnlyList<MaintenanceIssueResponse>>> Handle(
    GetMaintenanceIssuesQuery query, CancellationToken cancellationToken)
  {
    IQueryable<MaintenanceIssue> q = context.MaintenanceIssues.AsNoTracking();

    if (query.Status == MaintenanceIssueStatus.Open)
    {
      q = q.Where(m => m.ResolvedAtUtc == null);
    }
    else if (query.Status == MaintenanceIssueStatus.Resolved)
    {
      q = q.Where(m => m.ResolvedAtUtc != null);
    }

    if (query.SpotId.HasValue)
    {
      q = q.Where(m => m.SpotId == query.SpotId);
    }
    if (query.From.HasValue)
    {
      q = q.Where(m => m.IssuedAtUtc >= query.From);
    }
    if (query.To.HasValue)
    {
      q = q.Where(m => m.IssuedAtUtc <= query.To);
    }

    List<MaintenanceIssueResponse> list = await q
      .OrderByDescending(m => m.IssuedAtUtc)
      .Select(m => new MaintenanceIssueResponse(
        m.Id, m.SpotId, m.IssuedAtUtc, m.ProblemDescription,
        m.SolverUserId, m.ResolvedAtUtc, m.Note))
      .ToListAsync(cancellationToken);

    return list;
  }
}

public sealed record GetMaintenanceIssueQuery(Guid Id) : IQuery<MaintenanceIssueResponse>;

internal sealed class GetMaintenanceIssueQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetMaintenanceIssueQuery, MaintenanceIssueResponse>
{
  public async Task<Result<MaintenanceIssueResponse>> Handle(
    GetMaintenanceIssueQuery query, CancellationToken cancellationToken)
  {
    MaintenanceIssue? m = await context.MaintenanceIssues.AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);
    if (m is null)
    {
      return Result.Failure<MaintenanceIssueResponse>(MaintenanceIssueErrors.NotFound(query.Id));
    }
    return new MaintenanceIssueResponse(
      m.Id, m.SpotId, m.IssuedAtUtc, m.ProblemDescription,
      m.SolverUserId, m.ResolvedAtUtc, m.Note);
  }
}

public sealed record CreateMaintenanceIssueCommand(
  Guid? SpotId, string ProblemDescription, string? Note) : ICommand<Guid>;

internal sealed class CreateMaintenanceIssueCommandHandler(
  IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
  : ICommandHandler<CreateMaintenanceIssueCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateMaintenanceIssueCommand command, CancellationToken cancellationToken)
  {
    MaintenanceIssue issue = new()
    {
      Id = Guid.NewGuid(),
      SpotId = command.SpotId,
      IssuedAtUtc = dateTimeProvider.UtcNow,
      ProblemDescription = command.ProblemDescription,
      Note = command.Note
    };
    context.MaintenanceIssues.Add(issue);
    await context.SaveChangesAsync(cancellationToken);
    return issue.Id;
  }
}

internal sealed class CreateMaintenanceIssueCommandValidator : AbstractValidator<CreateMaintenanceIssueCommand>
{
  public CreateMaintenanceIssueCommandValidator()
  {
    RuleFor(c => c.ProblemDescription).NotEmpty().MaximumLength(2000);
    RuleFor(c => c.Note).MaximumLength(2000);
  }
}

public sealed record UpdateMaintenanceIssueCommand(
  Guid Id, string ProblemDescription, Guid? SolverUserId, string? Note) : ICommand;

internal sealed class UpdateMaintenanceIssueCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateMaintenanceIssueCommand>
{
  public async Task<Result> Handle(
    UpdateMaintenanceIssueCommand command, CancellationToken cancellationToken)
  {
    MaintenanceIssue? m = await context.MaintenanceIssues
      .FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken);
    if (m is null)
    {
      return Result.Failure(MaintenanceIssueErrors.NotFound(command.Id));
    }
    m.ProblemDescription = command.ProblemDescription;
    m.SolverUserId = command.SolverUserId;
    m.Note = command.Note;
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

internal sealed class UpdateMaintenanceIssueCommandValidator : AbstractValidator<UpdateMaintenanceIssueCommand>
{
  public UpdateMaintenanceIssueCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();
    RuleFor(c => c.ProblemDescription).NotEmpty().MaximumLength(2000);
    RuleFor(c => c.Note).MaximumLength(2000);
  }
}

public sealed record ResolveMaintenanceIssueCommand(Guid Id) : ICommand;

internal sealed class ResolveMaintenanceIssueCommandHandler(
  IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
  : ICommandHandler<ResolveMaintenanceIssueCommand>
{
  public async Task<Result> Handle(
    ResolveMaintenanceIssueCommand command, CancellationToken cancellationToken)
  {
    MaintenanceIssue? m = await context.MaintenanceIssues
      .FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken);
    if (m is null)
    {
      return Result.Failure(MaintenanceIssueErrors.NotFound(command.Id));
    }
    if (m.ResolvedAtUtc is not null)
    {
      return Result.Failure(MaintenanceIssueErrors.AlreadyResolved(command.Id));
    }
    m.ResolvedAtUtc = dateTimeProvider.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

public sealed record DeleteMaintenanceIssueCommand(Guid Id) : ICommand;

internal sealed class DeleteMaintenanceIssueCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteMaintenanceIssueCommand>
{
  public async Task<Result> Handle(
    DeleteMaintenanceIssueCommand command, CancellationToken cancellationToken)
  {
    MaintenanceIssue? m = await context.MaintenanceIssues
      .FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken);
    if (m is null)
    {
      return Result.Failure(MaintenanceIssueErrors.NotFound(command.Id));
    }
    context.MaintenanceIssues.Remove(m);
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
