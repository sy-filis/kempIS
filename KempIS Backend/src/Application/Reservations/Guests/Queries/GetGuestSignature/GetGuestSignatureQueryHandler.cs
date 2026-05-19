using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Guests.Queries.GetGuestSignature;

internal sealed class GetGuestSignatureQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGuestSignatureQuery, GetGuestSignatureResponse>
{
  public async Task<Result<GetGuestSignatureResponse>> Handle(
    GetGuestSignatureQuery query,
    CancellationToken cancellationToken)
  {
    byte[]? bytes = await context.Guests
      .AsNoTracking()
      .Where(g => g.Id == query.Id)
      .Select(g => g.SignaturePng)
      .SingleOrDefaultAsync(cancellationToken);

    if (bytes is null)
    {
      return Result.Failure<GetGuestSignatureResponse>(GuestErrors.SignatureNotFound(query.Id));
    }

    return Result.Success(new GetGuestSignatureResponse(bytes));
  }
}
