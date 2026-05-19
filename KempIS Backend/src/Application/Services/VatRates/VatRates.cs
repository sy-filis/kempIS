using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Services;
using Domain.Services.VatRates;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Services.VatRates;

public sealed record VatRateResponse(Guid Id, string Name, decimal Rate, bool IsActive);

public sealed record GetVatRatesQuery : IQuery<List<VatRateResponse>>;

internal sealed class GetVatRatesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetVatRatesQuery, List<VatRateResponse>>
{
  public async Task<Result<List<VatRateResponse>>> Handle(
    GetVatRatesQuery query,
    CancellationToken cancellationToken)
  {
    List<VatRateResponse> vatRates = await context.VatRates
      .Select(vr => new VatRateResponse(vr.Id, vr.Name, vr.Rate, vr.IsActive))
      .ToListAsync(cancellationToken);

    return vatRates;
  }
}

public sealed record CreateVatRateCommand(string Name, decimal Rate, bool IsActive) : ICommand<Guid>;

internal sealed class CreateVatRateCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateVatRateCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateVatRateCommand command,
    CancellationToken cancellationToken)
  {
    VatRate vatRate = new()
    {
      Id = Guid.NewGuid(),
      Name = command.Name,
      Rate = command.Rate,
      IsActive = command.IsActive
    };

    context.VatRates.Add(vatRate);
    await context.SaveChangesAsync(cancellationToken);

    return vatRate.Id;
  }
}

internal sealed class CreateVatRateCommandValidator : AbstractValidator<CreateVatRateCommand>
{
  public CreateVatRateCommandValidator()
  {
    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(100);

    RuleFor(c => c.Rate)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(100);
  }
}

public sealed record UpdateVatRateCommand(Guid Id, string Name, decimal Rate, bool IsActive) : ICommand;

internal sealed class UpdateVatRateCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateVatRateCommand>
{
  public async Task<Result> Handle(
    UpdateVatRateCommand command,
    CancellationToken cancellationToken)
  {
    VatRate? vatRate = await context.VatRates
      .FirstOrDefaultAsync(vr => vr.Id == command.Id, cancellationToken);

    if (vatRate is null)
    {
      return Result.Failure(VatRateErrors.NotFound(command.Id));
    }

    vatRate.Name = command.Name;
    vatRate.Rate = command.Rate;
    vatRate.IsActive = command.IsActive;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateVatRateCommandValidator : AbstractValidator<UpdateVatRateCommand>
{
  public UpdateVatRateCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(100);

    RuleFor(c => c.Rate)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(100);
  }
}

public sealed record DeleteVatRateCommand(Guid Id) : ICommand;

internal sealed class DeleteVatRateCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteVatRateCommand>
{
  public async Task<Result> Handle(
    DeleteVatRateCommand command,
    CancellationToken cancellationToken)
  {
    VatRate? vatRate = await context.VatRates
      .FirstOrDefaultAsync(vr => vr.Id == command.Id, cancellationToken);

    if (vatRate is null)
    {
      return Result.Failure(VatRateErrors.NotFound(command.Id));
    }

    context.VatRates.Remove(vatRate);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteVatRateCommandValidator : AbstractValidator<DeleteVatRateCommand>
{
  public DeleteVatRateCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}
