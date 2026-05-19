using Application.Abstractions.Data;
using Application.Abstractions.Finance;
using Application.Abstractions.Messaging;
using Application.Configuration;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Finance.Bills.CreateRepairBill;

internal sealed class CreateRepairBillCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IBillNumberGenerator numberGenerator,
  IOptions<RetentionSettings> retentionSettings)
  : ICommandHandler<CreateRepairBillCommand, CreateRepairBillResponse>
{
  public async Task<Result<CreateRepairBillResponse>> Handle(
    CreateRepairBillCommand command,
    CancellationToken cancellationToken)
  {
    Bill? original = await context.Bills
      .FirstOrDefaultAsync(b => b.Id == command.OriginalBillId, cancellationToken);

    if (original is null)
    {
      return Result.Failure<CreateRepairBillResponse>(BillErrors.NotFound(command.OriginalBillId));
    }

    if (original.Kind != BillKind.Regular)
    {
      return Result.Failure<CreateRepairBillResponse>(BillErrors.OriginalMustBeRegular);
    }

    List<BillItem> originalItems = await context.BillItems
      .Where(i => i.BillId == original.Id)
      .ToListAsync(cancellationToken);

    List<Guid> priorRepairIds = await context.Bills
      .Where(b => b.OriginalBillId == original.Id)
      .Select(b => b.Id)
      .ToListAsync(cancellationToken);

    List<BillItem> priorRepairItems = priorRepairIds.Count == 0
      ? []
      : await context.BillItems
        .Where(i => priorRepairIds.Contains(i.BillId))
        .ToListAsync(cancellationToken);

    static (Guid? ServiceId, decimal UnitPrice, decimal VatRate) Key(BillItem i) =>
      (i.ServiceId, i.UnitPrice, i.VatRatePercentage);

    // Repair items store negated UnitPrice; flip back to match the original's key space.
    static (Guid? ServiceId, decimal UnitPrice, decimal VatRate) RepairKey(BillItem i) =>
      (i.ServiceId, -i.UnitPrice, i.VatRatePercentage);

    Dictionary<(Guid?, decimal, decimal), uint> originalByKey = originalItems
      .GroupBy(Key)
      .ToDictionary(g => g.Key,
        g => (uint)g.Sum(x => (long)x.RecapSingleQuantity * x.RecapDayQuantity));

    Dictionary<(Guid?, decimal, decimal), uint> priorByKey = priorRepairItems
      .GroupBy(RepairKey)
      .ToDictionary(g => g.Key,
        g => (uint)g.Sum(x => (long)x.RecapSingleQuantity * x.RecapDayQuantity));

    var pendingByKey = new Dictionary<(Guid?, decimal, decimal), uint>();
    foreach (BillItemInput input in command.Items)
    {
      (Guid?, decimal, decimal) key = (input.ServiceId, input.UnitPrice, input.VatRatePercentage);

      if (!originalByKey.TryGetValue(key, out uint originalQty))
      {
        return Result.Failure<CreateRepairBillResponse>(BillErrors.RepairLineNotOnOriginal);
      }

      uint inputUnits = input.RecapSingleQuantity * input.RecapDayQuantity;
      uint priorQty = priorByKey.GetValueOrDefault(key, 0u);
      pendingByKey.TryGetValue(key, out uint pendingQty);

      uint cap = originalQty - priorQty - pendingQty;
      if (inputUnits > cap)
      {
        return Result.Failure<CreateRepairBillResponse>(
          BillErrors.RepairQuantityExceedsCap(inputUnits, cap));
      }

      pendingByKey[key] = pendingQty + inputUnits;
    }

    DateTime now = dateTimeProvider.UtcNow;
    string number = await numberGenerator.NextAsync(now.Year, cancellationToken);

    // Repair total is negative (reverses original). UnitPrice is gross; qty = recapSingle × recapDay.
    decimal repairTotal = -command.Items.Sum(i =>
      (decimal)i.RecapSingleQuantity * i.RecapDayQuantity * i.UnitPrice);

    var repairBill = new Bill
    {
      Id = Guid.NewGuid(),
      Number = number,
      Kind = BillKind.Repair,
      OriginalBillId = original.Id,
      RepairReason = command.Reason,
      ReservationId = original.ReservationId,
      LanguageIdGuid = original.LanguageIdGuid,
      IssuedAtUtc = now,
      CheckInAt = original.CheckInAt,
      CheckOutAt = original.CheckOutAt,
      Payer = new Payer
      {
        Name = original.Payer.Name,
        Surname = original.Payer.Surname,
        Address = new Address(
          original.Payer.Address.CountryId,
          original.Payer.Address.City,
          original.Payer.Address.ZipCode,
          original.Payer.Address.Street,
          original.Payer.Address.HouseNumber),
      },
      LegalEntity = original.LegalEntity is { } originalLegal
        ? new LegalEntity
        {
          Name = originalLegal.Name,
          Cin = originalLegal.Cin,
          Tin = originalLegal.Tin,
          Address = new Address(
            originalLegal.Address.CountryId,
            originalLegal.Address.City,
            originalLegal.Address.ZipCode,
            originalLegal.Address.Street,
            originalLegal.Address.HouseNumber),
        }
        : null,
      Payment = new Payment(command.PaymentType, repairTotal),
      Scartation = DateOnly.FromDateTime(now).AddYears(retentionSettings.Value.BillYears),
    };

    context.Bills.Add(repairBill);

    foreach (BillItemInput item in command.Items)
    {
      context.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = repairBill.Id,
        ServiceId = item.ServiceId,
        Quantity = item.Quantity,
        UnitPrice = -item.UnitPrice,
        VatRatePercentage = item.VatRatePercentage,
        RecapSingleQuantity = item.RecapSingleQuantity,
        RecapDayQuantity = item.RecapDayQuantity,
      });
    }

    repairBill.Raise(new BillCreatedDomainEvent(repairBill.Id));
    repairBill.Raise(new BillRepairedDomainEvent(original.Id, repairBill.Id, command.Reason));

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success(new CreateRepairBillResponse(repairBill.Id, number));
  }
}
