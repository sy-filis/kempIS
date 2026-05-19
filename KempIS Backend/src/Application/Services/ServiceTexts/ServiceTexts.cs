using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Services;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Services.ServiceTexts;

public sealed record ServiceTextResponse(Guid Id, Guid ServiceId, Guid LanguageId, string PrintText);

public sealed record GetServiceTextsQuery : IQuery<List<ServiceTextResponse>>;

internal sealed class GetServiceTextsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetServiceTextsQuery, List<ServiceTextResponse>>
{
  public async Task<Result<List<ServiceTextResponse>>> Handle(
    GetServiceTextsQuery query,
    CancellationToken cancellationToken)
  {
    List<ServiceTextResponse> serviceTexts = await context.ServiceTexts
      .Select(st => new ServiceTextResponse(st.Id, st.ServiceId, st.LanguageId, st.PrintText))
      .ToListAsync(cancellationToken);

    return serviceTexts;
  }
}

public sealed record CreateServiceTextCommand(Guid ServiceId, Guid LanguageId, string PrintText) : ICommand<Guid>;

internal sealed class CreateServiceTextCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateServiceTextCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateServiceTextCommand command,
    CancellationToken cancellationToken)
  {
    if (!await context.Services.AnyAsync(s => s.Id == command.ServiceId, cancellationToken))
    {
      return Result.Failure<Guid>(ServiceErrors.NotFound(command.ServiceId));
    }

    if (!await context.Languages.AnyAsync(l => l.Id == command.LanguageId, cancellationToken))
    {
      return Result.Failure<Guid>(LanguageErrors.NotFound(command.LanguageId));
    }

    bool exists = await context.ServiceTexts
      .AnyAsync(st => st.ServiceId == command.ServiceId && st.LanguageId == command.LanguageId, cancellationToken);

    if (exists)
    {
      return Result.Failure<Guid>(Error.Conflict(
        "ServiceTexts.AlreadyExists",
        "A service text for the given service and language already exists."));
    }

    ServiceText serviceText = new()
    {
      Id = Guid.NewGuid(),
      ServiceId = command.ServiceId,
      LanguageId = command.LanguageId,
      PrintText = command.PrintText
    };

    context.ServiceTexts.Add(serviceText);
    await context.SaveChangesAsync(cancellationToken);

    return serviceText.Id;
  }
}

internal sealed class CreateServiceTextCommandValidator : AbstractValidator<CreateServiceTextCommand>
{
  public CreateServiceTextCommandValidator()
  {
    RuleFor(c => c.ServiceId)
      .NotEmpty();

    RuleFor(c => c.LanguageId)
      .NotEmpty();

    RuleFor(c => c.PrintText)
      .NotEmpty()
      .MaximumLength(1000);
  }
}

public sealed record UpdateServiceTextCommand(Guid Id, Guid ServiceId, Guid LanguageId, string PrintText) : ICommand;

internal sealed class UpdateServiceTextCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateServiceTextCommand>
{
  public async Task<Result> Handle(
    UpdateServiceTextCommand command,
    CancellationToken cancellationToken)
  {
    ServiceText? serviceText = await context.ServiceTexts
      .FirstOrDefaultAsync(st => st.Id == command.Id, cancellationToken);

    if (serviceText is null)
    {
      return Result.Failure(ServiceTextErrors.NotFound(command.Id));
    }

    if (!await context.Services.AnyAsync(s => s.Id == command.ServiceId, cancellationToken))
    {
      return Result.Failure(ServiceErrors.NotFound(command.ServiceId));
    }

    if (!await context.Languages.AnyAsync(l => l.Id == command.LanguageId, cancellationToken))
    {
      return Result.Failure(LanguageErrors.NotFound(command.LanguageId));
    }

    bool exists = await context.ServiceTexts
      .AnyAsync(st =>
        st.Id != command.Id &&
        st.ServiceId == command.ServiceId &&
        st.LanguageId == command.LanguageId,
        cancellationToken);

    if (exists)
    {
      return Result.Failure(Error.Conflict(
        "ServiceTexts.AlreadyExists",
        "A service text for the given service and language already exists."));
    }

    serviceText.ServiceId = command.ServiceId;
    serviceText.LanguageId = command.LanguageId;
    serviceText.PrintText = command.PrintText;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateServiceTextCommandValidator : AbstractValidator<UpdateServiceTextCommand>
{
  public UpdateServiceTextCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.ServiceId)
      .NotEmpty();

    RuleFor(c => c.LanguageId)
      .NotEmpty();

    RuleFor(c => c.PrintText)
      .NotEmpty()
      .MaximumLength(1000);
  }
}

public sealed record DeleteServiceTextCommand(Guid Id) : ICommand;

internal sealed class DeleteServiceTextCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteServiceTextCommand>
{
  public async Task<Result> Handle(
    DeleteServiceTextCommand command,
    CancellationToken cancellationToken)
  {
    ServiceText? serviceText = await context.ServiceTexts
      .FirstOrDefaultAsync(st => st.Id == command.Id, cancellationToken);

    if (serviceText is null)
    {
      return Result.Failure(ServiceTextErrors.NotFound(command.Id));
    }

    context.ServiceTexts.Remove(serviceText);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteServiceTextCommandValidator : AbstractValidator<DeleteServiceTextCommand>
{
  public DeleteServiceTextCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
