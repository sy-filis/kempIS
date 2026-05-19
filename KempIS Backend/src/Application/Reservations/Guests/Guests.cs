using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Configuration;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Reservations.Guests;

public sealed record GuestResponse(
  Guid Id,
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  bool HasSignature,
  DateTime? SignatureCapturedAtUtc);

public sealed record GetGuestsQuery(DateOnly From, DateOnly To, string? Search)
  : IQuery<List<GuestResponse>>;

internal sealed class GetGuestsQueryValidator : AbstractValidator<GetGuestsQuery>
{
  public GetGuestsQueryValidator()
  {
    RuleFor(q => q.From).NotEmpty();
    RuleFor(q => q.To).NotEmpty().GreaterThanOrEqualTo(q => q.From);
    RuleFor(q => q.Search).MaximumLength(100).When(q => q.Search is not null);
  }
}

internal sealed class GetGuestsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGuestsQuery, List<GuestResponse>>
{
  public async Task<Result<List<GuestResponse>>> Handle(
    GetGuestsQuery query,
    CancellationToken cancellationToken)
  {
    IQueryable<Guest> queryable = context.Guests
      .AsNoTracking()
      .Where(g => g.BillId != null
               && context.Bills.Any(b => b.Id == g.BillId
                                      && b.CheckInAt <= query.To
                                      && b.CheckOutAt >= query.From));

    string? trimmed = query.Search?.Trim();
    if (!string.IsNullOrEmpty(trimmed))
    {
#pragma warning disable CA1304, CA1311, CA1862 // EF translates LOWER(col) LIKE '%p%' on both Npgsql and SQLite; StringComparison overloads are not supported in expression trees
      string pattern = trimmed.ToLower();
      queryable = queryable.Where(g =>
        g.FirstName.ToLower().Contains(pattern)
        || g.LastName.ToLower().Contains(pattern)
        || g.DocumentNumber != null && g.DocumentNumber.ToLower().Contains(pattern)
        || g.ReasonOfStay.ToLower().Contains(pattern)
        || g.VisaNumber != null && g.VisaNumber.ToLower().Contains(pattern)
        || g.Note != null && g.Note.ToLower().Contains(pattern)
        || g.Address.City.ToLower().Contains(pattern)
        || g.Address.Street.ToLower().Contains(pattern)
        || g.Address.ZipCode.ToLower().Contains(pattern)
        || g.Address.HouseNumber.ToLower().Contains(pattern));
#pragma warning restore CA1304, CA1311, CA1862
    }

    List<GuestResponse> guests = await queryable
      .Select(g => new GuestResponse(
        g.Id,
        g.ReservationId,
        g.BillId,
        g.PaysRecreationFee,
        g.FirstName,
        g.LastName,
        g.NationalityId,
        g.DateOfBirth,
        g.DocumentType,
        g.DocumentNumber,
        g.Address,
        g.ReasonOfStay,
        g.StayDateRange,
        g.VisaNumber,
        g.Note,
        g.Scartation,
        g.CheckInAt,
        g.CheckOutAt,
        g.SignaturePng != null,
        g.SignatureCapturedAtUtc))
      .ToListAsync(cancellationToken);

    return guests;
  }
}

public sealed record CreateGuestCommand(
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  string? SignaturePngBase64) : ICommand<Guid>;

internal sealed class CreateGuestCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IOptions<RetentionSettings> retention)
  : ICommandHandler<CreateGuestCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateGuestCommand command,
    CancellationToken cancellationToken)
  {
    byte[]? signaturePng = null;
    DateTime? signatureCapturedAtUtc = null;

    if (command.SignaturePngBase64 is not null)
    {
      string? alpha2 = await context.Nationalities
        .AsNoTracking()
        .Where(n => n.Id == command.NationalityId)
        .Select(n => n.Alpha2)
        .SingleOrDefaultAsync(cancellationToken);

      if (alpha2 is not null && GuestSignatureRules.RequiresSignature(alpha2))
      {
        signaturePng = Convert.FromBase64String(command.SignaturePngBase64);
        signatureCapturedAtUtc = dateTimeProvider.UtcNow;
      }
    }

    Guest guest = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = command.ReservationId,
      BillId = command.BillId,
      PaysRecreationFee = command.PaysRecreationFee,
      FirstName = command.FirstName,
      LastName = command.LastName,
      NationalityId = command.NationalityId,
      DateOfBirth = command.DateOfBirth,
      DocumentType = command.DocumentType,
      DocumentNumber = command.DocumentNumber,
      Address = command.Address,
      ReasonOfStay = command.ReasonOfStay,
      StayDateRange = command.StayDateRange,
      VisaNumber = command.VisaNumber,
      Note = command.Note,
      Scartation = command.Scartation ?? command.StayDateRange?.To.AddYears(retention.Value.GuestYears),
      CheckInAt = command.CheckInAt,
      CheckOutAt = command.CheckOutAt,
      SignaturePng = signaturePng,
      SignatureCapturedAtUtc = signatureCapturedAtUtc,
    };

    context.Guests.Add(guest);
    await context.SaveChangesAsync(cancellationToken);

    return guest.Id;
  }
}

internal sealed class CreateGuestCommandValidator : AbstractValidator<CreateGuestCommand>
{
  public CreateGuestCommandValidator()
  {
    RuleFor(c => c.BillId)
      .NotEmpty()
      .When(c => c.BillId.HasValue);

    RuleFor(c => c.PaysRecreationFee)
      .NotNull()
      .When(c => c.BillId.HasValue)
      .WithErrorCode("Guest.PaysRecreationFeeRequiredWhenBilled")
      .WithMessage("PaysRecreationFee must be set when the guest is linked to a bill.");

    RuleFor(c => c.PaysRecreationFee)
      .Null()
      .When(c => !c.BillId.HasValue)
      .WithErrorCode("Guest.PaysRecreationFeeForbiddenWithoutBill")
      .WithMessage("PaysRecreationFee may only be set when the guest is linked to a bill.");

    RuleFor(c => c.FirstName)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.LastName)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.NationalityId)
      .NotEmpty();

    RuleFor(c => c.DateOfBirth)
      .NotEmpty();

    RuleFor(c => c.DocumentType)
      .IsInEnum()
      .When(c => c.DocumentType.HasValue);

    RuleFor(c => c.DocumentNumber)
      .MaximumLength(50)
      .When(c => c.DocumentNumber is not null);

    RuleFor(c => c)
      .Must(c => c.DocumentType.HasValue || c.DocumentNumber is null)
      .WithMessage("DocumentNumber must be null when DocumentType is null.");

    RuleFor(c => c.Address)
      .NotNull();

    RuleFor(c => c.Address.CountryId)
      .NotEmpty();

    RuleFor(c => c.Address.City)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Address.ZipCode)
      .NotEmpty()
      .MaximumLength(16);

    RuleFor(c => c.Address.Street)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Address.HouseNumber)
      .NotEmpty()
      .MaximumLength(16);

    RuleFor(c => c.ReasonOfStay)
      .MaximumLength(500);

    RuleFor(c => c.StayDateRange!.To)
      .GreaterThanOrEqualTo(c => c.StayDateRange!.From)
      .When(c => c.StayDateRange is not null);

    RuleFor(c => c.VisaNumber)
      .MaximumLength(50)
      .When(c => c.VisaNumber is not null);

    RuleFor(c => c.Note)
      .MaximumLength(1000)
      .When(c => c.Note is not null);

    RuleFor(c => c.SignaturePngBase64).ValidPngBase64();
  }
}

public sealed record UpdateGuestCommand(
  Guid Id,
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  string? SignaturePngBase64) : ICommand;

internal sealed class UpdateGuestCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<UpdateGuestCommand>
{
  public async Task<Result> Handle(
    UpdateGuestCommand command,
    CancellationToken cancellationToken)
  {
    Guest? guest = await context.Guests
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (guest is null)
    {
      return Result.Failure(GuestErrors.NotFound(command.Id));
    }

    guest.ReservationId = command.ReservationId;
    guest.BillId = command.BillId;
    guest.PaysRecreationFee = command.PaysRecreationFee;
    guest.FirstName = command.FirstName;
    guest.LastName = command.LastName;
    guest.NationalityId = command.NationalityId;
    guest.DateOfBirth = command.DateOfBirth;
    guest.DocumentType = command.DocumentType;
    guest.DocumentNumber = command.DocumentNumber;
    guest.Address = command.Address;
    guest.ReasonOfStay = command.ReasonOfStay;
    guest.StayDateRange = command.StayDateRange;
    guest.VisaNumber = command.VisaNumber;
    guest.Note = command.Note;
    guest.Scartation = command.Scartation ?? guest.Scartation;
    guest.CheckInAt = command.CheckInAt;
    guest.CheckOutAt = command.CheckOutAt;

    if (command.SignaturePngBase64 is not null)
    {
      string? alpha2 = await context.Nationalities
        .AsNoTracking()
        .Where(n => n.Id == command.NationalityId)
        .Select(n => n.Alpha2)
        .SingleOrDefaultAsync(cancellationToken);

      if (alpha2 is not null && GuestSignatureRules.RequiresSignature(alpha2))
      {
        guest.SignaturePng = Convert.FromBase64String(command.SignaturePngBase64);
        guest.SignatureCapturedAtUtc = dateTimeProvider.UtcNow;
      }
      else
      {
        guest.SignaturePng = null;
        guest.SignatureCapturedAtUtc = null;
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateGuestCommandValidator : AbstractValidator<UpdateGuestCommand>
{
  public UpdateGuestCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.BillId)
      .NotEmpty()
      .When(c => c.BillId.HasValue);

    RuleFor(c => c.PaysRecreationFee)
      .NotNull()
      .When(c => c.BillId.HasValue)
      .WithErrorCode("Guest.PaysRecreationFeeRequiredWhenBilled")
      .WithMessage("PaysRecreationFee must be set when the guest is linked to a bill.");

    RuleFor(c => c.PaysRecreationFee)
      .Null()
      .When(c => !c.BillId.HasValue)
      .WithErrorCode("Guest.PaysRecreationFeeForbiddenWithoutBill")
      .WithMessage("PaysRecreationFee may only be set when the guest is linked to a bill.");

    RuleFor(c => c.FirstName)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.LastName)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.NationalityId)
      .NotEmpty();

    RuleFor(c => c.DateOfBirth)
      .NotEmpty();

    RuleFor(c => c.DocumentType)
      .IsInEnum()
      .When(c => c.DocumentType.HasValue);

    RuleFor(c => c.DocumentNumber)
      .MaximumLength(50)
      .When(c => c.DocumentNumber is not null);

    RuleFor(c => c)
      .Must(c => c.DocumentType.HasValue || c.DocumentNumber is null)
      .WithMessage("DocumentNumber must be null when DocumentType is null.");

    RuleFor(c => c.Address)
      .NotNull();

    RuleFor(c => c.Address.CountryId)
      .NotEmpty();

    RuleFor(c => c.Address.City)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Address.ZipCode)
      .NotEmpty()
      .MaximumLength(16);

    RuleFor(c => c.Address.Street)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.Address.HouseNumber)
      .NotEmpty()
      .MaximumLength(16);

    RuleFor(c => c.ReasonOfStay)
      .MaximumLength(500);

    RuleFor(c => c.StayDateRange!.To)
      .GreaterThanOrEqualTo(c => c.StayDateRange!.From)
      .When(c => c.StayDateRange is not null);

    RuleFor(c => c.VisaNumber)
      .MaximumLength(50)
      .When(c => c.VisaNumber is not null);

    RuleFor(c => c.Note)
      .MaximumLength(1000)
      .When(c => c.Note is not null);

    RuleFor(c => c.SignaturePngBase64).ValidPngBase64();
  }
}

public sealed record DeleteGuestCommand(Guid Id) : ICommand;

internal sealed class DeleteGuestCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteGuestCommand>
{
  public async Task<Result> Handle(
    DeleteGuestCommand command,
    CancellationToken cancellationToken)
  {
    Guest? guest = await context.Guests
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (guest is null)
    {
      return Result.Failure(GuestErrors.NotFound(command.Id));
    }

    context.Guests.Remove(guest);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteGuestCommandValidator : AbstractValidator<DeleteGuestCommand>
{
  public DeleteGuestCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}

public sealed record GetInCampGuestCountQuery : IQuery<int>;

internal sealed class GetInCampGuestCountQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetInCampGuestCountQuery, int>
{
  public async Task<Result<int>> Handle(
    GetInCampGuestCountQuery query,
    CancellationToken cancellationToken)
  {
    int count = await context.Guests
      .CountAsync(g => g.CheckInAt != null && g.CheckOutAt == null, cancellationToken);

    return count;
  }
}

public sealed record GuestDetailResponse(
  Guid Id,
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  bool HasSignature,
  DateTime? SignatureCapturedAtUtc,
  DateTime CreatedAt,
  DateTime UpdatedAt,
  DateTime? ReportedAt);

public sealed record GetGuestByIdQuery(Guid Id) : IQuery<GuestDetailResponse>;

internal sealed class GetGuestByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGuestByIdQuery, GuestDetailResponse>
{
  public async Task<Result<GuestDetailResponse>> Handle(
    GetGuestByIdQuery query,
    CancellationToken cancellationToken)
  {
    GuestDetailResponse? response = await context.Guests
      .AsNoTracking()
      .Where(g => g.Id == query.Id)
      .Select(g => new GuestDetailResponse(
        g.Id,
        g.ReservationId,
        g.BillId,
        g.PaysRecreationFee,
        g.FirstName,
        g.LastName,
        g.NationalityId,
        g.DateOfBirth,
        g.DocumentType,
        g.DocumentNumber,
        g.Address,
        g.ReasonOfStay,
        g.StayDateRange,
        g.VisaNumber,
        g.Note,
        g.Scartation,
        g.CheckInAt,
        g.CheckOutAt,
        g.SignaturePng != null,
        g.SignatureCapturedAtUtc,
        g.CreatedAt,
        g.UpdatedAt,
        g.ReportedAt))
      .FirstOrDefaultAsync(cancellationToken);

    if (response is null)
    {
      return Result.Failure<GuestDetailResponse>(GuestErrors.NotFound(query.Id));
    }

    return Result.Success(response);
  }
}
