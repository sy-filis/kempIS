using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Guests.Commands.ClearGuestSignature;

internal sealed class ClearGuestSignatureCommandHandler(IApplicationDbContext context)
  : ICommandHandler<ClearGuestSignatureCommand>
{
  public async Task<Result> Handle(
    ClearGuestSignatureCommand command,
    CancellationToken cancellationToken)
  {
    Guest? guest = await context.Guests
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (guest is null)
    {
      return Result.Failure(GuestErrors.NotFound(command.Id));
    }

    guest.SignaturePng = null;
    guest.SignatureCapturedAtUtc = null;
    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
