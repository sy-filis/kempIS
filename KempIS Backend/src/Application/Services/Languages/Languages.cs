using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Services;
using Domain.Services.Languages;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Services.Languages;

public sealed record LanguageResponse(Guid Id, string Code, string Name);

public sealed record GetLanguagesQuery : IQuery<List<LanguageResponse>>;

internal sealed class GetLanguagesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetLanguagesQuery, List<LanguageResponse>>
{
  public async Task<Result<List<LanguageResponse>>> Handle(
    GetLanguagesQuery query,
    CancellationToken cancellationToken)
  {
    List<LanguageResponse> languages = await context.Languages
      .Select(l => new LanguageResponse(l.Id, l.Code, l.Name))
      .ToListAsync(cancellationToken);

    return languages;
  }
}

public sealed record CreateLanguageCommand(string Code, string Name) : ICommand<Guid>;

internal sealed class CreateLanguageCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateLanguageCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateLanguageCommand command,
    CancellationToken cancellationToken)
  {
    bool codeExists = await context.Languages
      .AnyAsync(l => l.Code == command.Code, cancellationToken);

    if (codeExists)
    {
      return Result.Failure<Guid>(Error.Conflict(
        "Languages.CodeExists",
        $"A language with code '{command.Code}' already exists."));
    }

    Language language = new()
    {
      Id = Guid.NewGuid(),
      Code = command.Code,
      Name = command.Name
    };

    context.Languages.Add(language);
    await context.SaveChangesAsync(cancellationToken);

    return language.Id;
  }
}

internal sealed class CreateLanguageCommandValidator : AbstractValidator<CreateLanguageCommand>
{
  public CreateLanguageCommandValidator()
  {
    RuleFor(c => c.Code)
      .NotEmpty()
      .MaximumLength(10);

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(100);
  }
}

public sealed record UpdateLanguageCommand(Guid Id, string Code, string Name) : ICommand;

internal sealed class UpdateLanguageCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateLanguageCommand>
{
  public async Task<Result> Handle(
    UpdateLanguageCommand command,
    CancellationToken cancellationToken)
  {
    Language? language = await context.Languages
      .FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);

    if (language is null)
    {
      return Result.Failure(LanguageErrors.NotFound(command.Id));
    }

    bool codeExists = await context.Languages
      .AnyAsync(l => l.Id != command.Id && l.Code == command.Code, cancellationToken);

    if (codeExists)
    {
      return Result.Failure(Error.Conflict(
        "Languages.CodeExists",
        $"A language with code '{command.Code}' already exists."));
    }

    language.Code = command.Code;
    language.Name = command.Name;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateLanguageCommandValidator : AbstractValidator<UpdateLanguageCommand>
{
  public UpdateLanguageCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.Code)
      .NotEmpty()
      .MaximumLength(10);

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(100);
  }
}

public sealed record DeleteLanguageCommand(Guid Id) : ICommand;

internal sealed class DeleteLanguageCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteLanguageCommand>
{
  public async Task<Result> Handle(
    DeleteLanguageCommand command,
    CancellationToken cancellationToken)
  {
    Language? language = await context.Languages
      .FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);

    if (language is null)
    {
      return Result.Failure(LanguageErrors.NotFound(command.Id));
    }

    bool referenced = await context.Nationalities
      .AnyAsync(n => n.LanguageId == command.Id, cancellationToken);

    if (referenced)
    {
      return Result.Failure(LanguageErrors.HasReferences(command.Id));
    }

    context.Languages.Remove(language);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteLanguageCommandValidator : AbstractValidator<DeleteLanguageCommand>
{
  public DeleteLanguageCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
