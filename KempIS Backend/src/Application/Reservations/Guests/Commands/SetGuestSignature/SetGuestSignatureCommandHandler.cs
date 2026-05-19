using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Guests.Commands.SetGuestSignature;

internal sealed class SetGuestSignatureCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<SetGuestSignatureCommand>
{
  public async Task<Result> Handle(
    SetGuestSignatureCommand command,
    CancellationToken cancellationToken)
  {
    Guest? guest = await context.Guests
      .Include(g => g.Nationality)
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (guest is null)
    {
      return Result.Failure(GuestErrors.NotFound(command.Id));
    }

    if (!GuestSignatureRules.RequiresSignature(guest.Nationality!.Alpha2))
    {
      guest.SignaturePng = null;
      guest.SignatureCapturedAtUtc = null;
      await context.SaveChangesAsync(cancellationToken);
      return Result.Success();
    }

    guest.SignaturePng = Convert.FromBase64String(command.SignaturePngBase64);
    guest.SignatureCapturedAtUtc = dateTimeProvider.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
