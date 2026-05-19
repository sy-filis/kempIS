using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Nationalities;
using Domain.Services;
using Domain.Services.Languages;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Nationalities;

public sealed record NationalityResponse(
  Guid Id,
  string Name,
  string NameEn,
  string Alpha2,
  string Alpha3,
  string Numeric,
  bool VisaRequired,
  bool BiometricsRequired,
  bool IsEu,
  Guid LanguageId,
  string LanguageCode);

public sealed record GetNationalitiesQuery : IQuery<List<NationalityResponse>>;

internal sealed class GetNationalitiesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetNationalitiesQuery, List<NationalityResponse>>
{
  public async Task<Result<List<NationalityResponse>>> Handle(
    GetNationalitiesQuery query,
    CancellationToken cancellationToken)
  {
    List<NationalityResponse> nationalities = await context.Nationalities
      .AsNoTracking()
      .OrderBy(n => n.Name)
      .Join(
        context.Languages.AsNoTracking(),
        n => n.LanguageId,
        l => l.Id,
        (n, l) => new NationalityResponse(
          n.Id,
          n.Name,
          n.NameEn,
          n.Alpha2,
          n.Alpha3,
          n.Numeric,
          n.VisaRequired,
          n.BiometricsRequired,
          n.IsEu,
          n.LanguageId,
          l.Code))
      .ToListAsync(cancellationToken);

    return nationalities;
  }
}

public sealed record CreateNationalityCommand(
  string Name,
  string NameEn,
  string Alpha2,
  string Alpha3,
  string Numeric,
  bool VisaRequired,
  bool BiometricsRequired,
  bool IsEu,
  Guid LanguageId) : ICommand<Guid>;

internal sealed class CreateNationalityCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateNationalityCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateNationalityCommand command,
    CancellationToken cancellationToken)
  {
    bool languageExists = await context.Languages
      .AnyAsync(l => l.Id == command.LanguageId, cancellationToken);

    if (!languageExists)
    {
      return Result.Failure<Guid>(LanguageErrors.NotFound(command.LanguageId));
    }

    if (await context.Nationalities.AnyAsync(n => n.Alpha2 == command.Alpha2, cancellationToken))
    {
      return Result.Failure<Guid>(NationalityErrors.Alpha2Exists(command.Alpha2));
    }

    if (await context.Nationalities.AnyAsync(n => n.Alpha3 == command.Alpha3, cancellationToken))
    {
      return Result.Failure<Guid>(NationalityErrors.Alpha3Exists(command.Alpha3));
    }

    if (await context.Nationalities.AnyAsync(n => n.Numeric == command.Numeric, cancellationToken))
    {
      return Result.Failure<Guid>(NationalityErrors.NumericExists(command.Numeric));
    }

    Nationality nationality = new()
    {
      Id = Guid.NewGuid(),
      Name = command.Name,
      NameEn = command.NameEn,
      Alpha2 = command.Alpha2,
      Alpha3 = command.Alpha3,
      Numeric = command.Numeric,
      VisaRequired = command.VisaRequired,
      BiometricsRequired = command.BiometricsRequired,
      IsEu = command.IsEu,
      LanguageId = command.LanguageId,
    };

    context.Nationalities.Add(nationality);
    await context.SaveChangesAsync(cancellationToken);

    return nationality.Id;
  }
}

internal sealed class CreateNationalityCommandValidator : AbstractValidator<CreateNationalityCommand>
{
  public CreateNationalityCommandValidator()
  {
    RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    RuleFor(c => c.NameEn).NotEmpty().MaximumLength(100);
    RuleFor(c => c.Alpha2).NotEmpty().Length(2);
    RuleFor(c => c.Alpha3).NotEmpty().Length(3);
    RuleFor(c => c.Numeric).NotEmpty().Length(3);
    RuleFor(c => c.LanguageId).NotEmpty();
  }
}

public sealed record UpdateNationalityCommand(
  Guid Id,
  string Name,
  string NameEn,
  string Alpha2,
  string Alpha3,
  string Numeric,
  bool VisaRequired,
  bool BiometricsRequired,
  bool IsEu,
  Guid LanguageId) : ICommand;

internal sealed class UpdateNationalityCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateNationalityCommand>
{
  public async Task<Result> Handle(
    UpdateNationalityCommand command,
    CancellationToken cancellationToken)
  {
    Nationality? nationality = await context.Nationalities
      .FirstOrDefaultAsync(n => n.Id == command.Id, cancellationToken);

    if (nationality is null)
    {
      return Result.Failure(NationalityErrors.NotFound(command.Id));
    }

    bool languageExists = await context.Languages
      .AnyAsync(l => l.Id == command.LanguageId, cancellationToken);

    if (!languageExists)
    {
      return Result.Failure(LanguageErrors.NotFound(command.LanguageId));
    }

    if (await context.Nationalities.AnyAsync(
      n => n.Id != command.Id && n.Alpha2 == command.Alpha2, cancellationToken))
    {
      return Result.Failure(NationalityErrors.Alpha2Exists(command.Alpha2));
    }

    if (await context.Nationalities.AnyAsync(
      n => n.Id != command.Id && n.Alpha3 == command.Alpha3, cancellationToken))
    {
      return Result.Failure(NationalityErrors.Alpha3Exists(command.Alpha3));
    }

    if (await context.Nationalities.AnyAsync(
      n => n.Id != command.Id && n.Numeric == command.Numeric, cancellationToken))
    {
      return Result.Failure(NationalityErrors.NumericExists(command.Numeric));
    }

    nationality.Name = command.Name;
    nationality.NameEn = command.NameEn;
    nationality.Alpha2 = command.Alpha2;
    nationality.Alpha3 = command.Alpha3;
    nationality.Numeric = command.Numeric;
    nationality.VisaRequired = command.VisaRequired;
    nationality.BiometricsRequired = command.BiometricsRequired;
    nationality.IsEu = command.IsEu;
    nationality.LanguageId = command.LanguageId;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateNationalityCommandValidator : AbstractValidator<UpdateNationalityCommand>
{
  public UpdateNationalityCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();
    RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    RuleFor(c => c.NameEn).NotEmpty().MaximumLength(100);
    RuleFor(c => c.Alpha2).NotEmpty().Length(2);
    RuleFor(c => c.Alpha3).NotEmpty().Length(3);
    RuleFor(c => c.Numeric).NotEmpty().Length(3);
    RuleFor(c => c.LanguageId).NotEmpty();
  }
}

public sealed record DeleteNationalityCommand(Guid Id) : ICommand;

internal sealed class DeleteNationalityCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteNationalityCommand>
{
  public async Task<Result> Handle(
    DeleteNationalityCommand command,
    CancellationToken cancellationToken)
  {
    Nationality? nationality = await context.Nationalities
      .FirstOrDefaultAsync(n => n.Id == command.Id, cancellationToken);

    if (nationality is null)
    {
      return Result.Failure(NationalityErrors.NotFound(command.Id));
    }

    bool referenced = await context.Guests
      .AnyAsync(g => g.NationalityId == command.Id, cancellationToken);

    if (referenced)
    {
      return Result.Failure(NationalityErrors.HasReferences(command.Id));
    }

    context.Nationalities.Remove(nationality);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteNationalityCommandValidator : AbstractValidator<DeleteNationalityCommand>
{
  public DeleteNationalityCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();
  }
}
