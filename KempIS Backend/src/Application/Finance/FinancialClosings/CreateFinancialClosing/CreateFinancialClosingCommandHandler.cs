using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.FinancialClosings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FinancialClosings.CreateFinancialClosing;

internal sealed class CreateFinancialClosingCommandHandler(
  IApplicationDbContext context,
  IFinancialClosingReportRenderer renderer,
  IDateTimeProvider dateTimeProvider,
  IUserContext userContext)
  : ICommandHandler<CreateFinancialClosingCommand, CreateFinancialClosingResponse>
{
  private sealed record OpenBillsAggregate(int Count, decimal Total);

  public async Task<Result<CreateFinancialClosingResponse>> Handle(
    CreateFinancialClosingCommand command,
    CancellationToken cancellationToken)
  {
    OpenBillsAggregate? aggregate = await context.Bills
      .Where(b => b.FinancialClosingId == null)
      .GroupBy(_ => 1)
      .Select(g => new OpenBillsAggregate(g.Count(), g.Sum(b => b.Payment.Amount)))
      .FirstOrDefaultAsync(cancellationToken);

    if (aggregate is null || aggregate.Count == 0)
    {
      return Result.Failure<CreateFinancialClosingResponse>(FinancialClosingErrors.NoOpenBills());
    }

    // ReadCommitted may race two callers on the same max; the unique index on
    // FinancialClosing.FinancialClosingId forces the loser to retry.
    uint nextId = await NextSequentialIdAsync(context, cancellationToken);

    var closing = FinancialClosing.Close(
      nextId,
      dateTimeProvider.UtcNow,
      aggregate.Total,
      userContext.UserId);

    context.FinancialClosings.Add(closing);
    await context.SaveChangesAsync(cancellationToken);

    Guid closingId = closing.Id;
    await context.Bills
      .Where(b => b.FinancialClosingId == null)
      .ExecuteUpdateAsync(
        s => s.SetProperty(b => b.FinancialClosingId, _ => closingId),
        cancellationToken);

    Result<byte[]> rendered = await renderer.RenderAsync(closing, cancellationToken);
    if (rendered.IsFailure)
    {
      return Result.Failure<CreateFinancialClosingResponse>(rendered.Error);
    }

    closing.DocumentContent = rendered.Value;
    closing.DocumentGeneratedAtUtc = dateTimeProvider.UtcNow;
    await context.SaveChangesAsync(cancellationToken);

    return new CreateFinancialClosingResponse(closing.Id, closing.FinancialClosingId, aggregate.Total, aggregate.Count);
  }

  private static async Task<uint> NextSequentialIdAsync(IApplicationDbContext context, CancellationToken ct)
  {
    uint max = await context.FinancialClosings
      .Select(c => (uint?)c.FinancialClosingId)
      .MaxAsync(ct) ?? 0u;
    return max + 1u;
  }
}
