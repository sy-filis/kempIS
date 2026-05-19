using Application.Abstractions.Data;
using Application.Abstractions.Finance;
using Application.Abstractions.Gate;
using Application.Abstractions.Messaging;
using Application.Configuration;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Domain.Reservations.Guests;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.Vehicles;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Finance.Bills.CreateBill;

internal sealed class CreateBillCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IBillNumberGenerator numberGenerator,
  IOptions<CampSettings> campSettings,
  IOptions<RetentionSettings> retentionSettings,
  IGateClient gateClient,
  ILogger<CreateBillCommandHandler> logger)
  : ICommandHandler<CreateBillCommand, CreateBillResponse>
{
  private static readonly TimeZoneInfo CampTimeZone =
    TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

  public async Task<Result<CreateBillResponse>> Handle(
    CreateBillCommand command,
    CancellationToken cancellationToken)
  {
    if (command.LinkedInvoiceIds.Distinct().Count() != command.LinkedInvoiceIds.Count)
    {
      return Result.Failure<CreateBillResponse>(BillErrors.DuplicateInvoiceIds);
    }

    if (command.ExistingGuests.Select(g => g.GuestId).Distinct().Count() != command.ExistingGuests.Count)
    {
      return Result.Failure<CreateBillResponse>(BillErrors.DuplicateGuestIds);
    }

    List<Invoice> linkedInvoices = command.LinkedInvoiceIds.Count == 0
      ? []
      : await context.Invoices
        .Where(i => command.LinkedInvoiceIds.Contains(i.Id))
        .ToListAsync(cancellationToken);

    if (linkedInvoices.Count != command.LinkedInvoiceIds.Count)
    {
      Guid missing = command.LinkedInvoiceIds.Except(linkedInvoices.Select(i => i.Id)).First();
      return Result.Failure<CreateBillResponse>(InvoiceErrors.NotFound(missing));
    }

    foreach (Invoice invoice in linkedInvoices)
    {
      if (invoice.Status != InvoiceStatus.Paid)
      {
        return Result.Failure<CreateBillResponse>(InvoiceErrors.NotPaid);
      }

      if (invoice.LinkedBillId is not null)
      {
        return Result.Failure<CreateBillResponse>(InvoiceErrors.AlreadyLinkedToBill);
      }

      if (invoice.ReservationId != command.ReservationId)
      {
        return Result.Failure<CreateBillResponse>(InvoiceErrors.ReservationMismatch);
      }
    }

    var existingGuestIds = command.ExistingGuests.Select(g => g.GuestId).ToList();
    var paysFeeByGuestId = command.ExistingGuests
      .ToDictionary(g => g.GuestId, g => g.PaysRecreationFee);

    List<Guest> existingGuests = existingGuestIds.Count == 0
      ? []
      : await context.Guests
        .Where(g => existingGuestIds.Contains(g.Id))
        .ToListAsync(cancellationToken);

    if (existingGuests.Count != existingGuestIds.Count)
    {
      return Result.Failure<CreateBillResponse>(
        Error.NotFound("Guest.NotFound", "One or more guests were not found."));
    }

    if (existingGuests.Any(g => g.BillId is not null))
    {
      return Result.Failure<CreateBillResponse>(BillErrors.GuestAlreadyLinkedToAnotherBill);
    }

    List<ReservationSpotItem> linkedSpotItems = command.ReservationSpotItemIds.Count == 0
      ? []
      : await context.ReservationSpotItems
        .Where(s => command.ReservationSpotItemIds.Contains(s.Id))
        .ToListAsync(cancellationToken);

    if (linkedSpotItems.Count != command.ReservationSpotItemIds.Count)
    {
      Guid missing = command.ReservationSpotItemIds.Except(linkedSpotItems.Select(s => s.Id)).First();
      return Result.Failure<CreateBillResponse>(
        Error.NotFound("ReservationSpotItem.NotFound",
          $"The ReservationSpotItem with the Id = '{missing}' was not found."));
    }

    if (linkedSpotItems.Any(s => s.ReservationId != command.ReservationId))
    {
      return Result.Failure<CreateBillResponse>(BillErrors.SpotItemNotInReservation);
    }

    if (linkedSpotItems.Any(s => s.BillId is not null))
    {
      return Result.Failure<CreateBillResponse>(BillErrors.SpotItemAlreadyLinkedToAnotherBill);
    }

    // Existing UIDs are transferred to this bill and overwritten with new input.
    Dictionary<ulong, AccessCard> existingCardsByUid = command.AccessCards.Count == 0
      ? []
      : await context.AccessCards
          .Where(c => command.AccessCards.Select(x => x.Uid).Contains(c.Uid))
          .ToDictionaryAsync(c => c.Uid, cancellationToken);

    List<Vehicle> linkedVehicles = command.ExistingVehicleIds.Count == 0
      ? []
      : await context.Vehicles
        .Where(v => command.ExistingVehicleIds.Contains(v.Id))
        .ToListAsync(cancellationToken);

    if (linkedVehicles.Count != command.ExistingVehicleIds.Count)
    {
      Guid missing = command.ExistingVehicleIds.Except(linkedVehicles.Select(v => v.Id)).First();
      return Result.Failure<CreateBillResponse>(VehicleErrors.NotFound(missing));
    }

    if (linkedVehicles.Any(v => v.BillId is not null))
    {
      return Result.Failure<CreateBillResponse>(BillErrors.VehicleAlreadyLinkedToAnotherBill);
    }

    if (linkedVehicles.Any(v => v.ReservationId != command.ReservationId))
    {
      return Result.Failure<CreateBillResponse>(BillErrors.VehicleNotInReservation);
    }

    // Snapshot VAT from the catalogue per Service; ad-hoc items keep the caller's value.
    List<Guid> referencedServiceIds = [.. command.Items
      .Where(i => i.ServiceId.HasValue)
      .Select(i => i.ServiceId!.Value)
      .Distinct()];

    Dictionary<Guid, decimal> vatByServiceId = referencedServiceIds.Count == 0
      ? []
      : await (
          from s in context.Services
          where referencedServiceIds.Contains(s.Id)
          join v in context.VatRates on s.VatRateId equals v.Id
          select new { s.Id, v.Rate }
        ).ToDictionaryAsync(x => x.Id, x => x.Rate, cancellationToken);

    if (vatByServiceId.Count != referencedServiceIds.Count)
    {
      Guid missing = referencedServiceIds.First(id => !vatByServiceId.ContainsKey(id));
      return Result.Failure<CreateBillResponse>(ServiceErrors.NotFound(missing));
    }

    decimal VatRateFor(BillItemInput item) =>
      item.ServiceId is { } sid ? vatByServiceId[sid] : item.VatRatePercentage;

    DateTime now = dateTimeProvider.UtcNow;
    var guestCheckOutAt = command.CheckOutAt.ToDateTime(campSettings.Value.CheckOutTime, DateTimeKind.Utc);
    string number = await numberGenerator.NextAsync(now.Year, cancellationToken);

    decimal deductionsTotal = 0m;
    foreach (Invoice invoice in linkedInvoices)
    {
      deductionsTotal += await ComputeInvoiceTotalAsync(context, invoice.Id, cancellationToken);
    }

    // UnitPrice is gross; billed quantity = recapSingle × recapDay (Quantity is a FE display field).
    decimal itemsTotal = command.Items.Sum(i =>
      (decimal)i.RecapSingleQuantity * i.RecapDayQuantity * i.UnitPrice);

    if (deductionsTotal > itemsTotal)
    {
      return Result.Failure<CreateBillResponse>(BillErrors.DeductionsExceedItemsTotal);
    }

    var bill = new Bill
    {
      Id = Guid.NewGuid(),
      Number = number,
      Kind = BillKind.Regular,
      OriginalBillId = null,
      ReservationId = command.ReservationId,
      LanguageIdGuid = command.LanguageId,
      IssuedAtUtc = now,
      CheckInAt = command.CheckInAt,
      CheckOutAt = command.CheckOutAt,
      Payer = new Payer
      {
        Name = command.Payer.Name,
        Surname = command.Payer.Surname,
        Address = command.Payer.Address,
      },
      LegalEntity = command.LegalEntity is { } legalInput
        ? new LegalEntity
        {
          Name = legalInput.Name,
          Cin = legalInput.Cin,
          Tin = legalInput.Tin,
          Address = legalInput.Address,
        }
        : null,
      Payment = new Payment(command.PaymentType, itemsTotal - deductionsTotal),
      Scartation = DateOnly.FromDateTime(now).AddYears(retentionSettings.Value.BillYears),
    };

    context.Bills.Add(bill);

    foreach (BillItemInput item in command.Items)
    {
      context.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = bill.Id,
        ServiceId = item.ServiceId,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        VatRatePercentage = VatRateFor(item),
        RecapSingleQuantity = item.RecapSingleQuantity,
        RecapDayQuantity = item.RecapDayQuantity,
      });
    }

    foreach (Invoice invoice in linkedInvoices)
    {
      invoice.LinkedBillId = bill.Id;
      invoice.Raise(new InvoiceLinkedToBillDomainEvent(invoice.Id, bill.Id));
    }

    foreach (Guest guest in existingGuests)
    {
      guest.BillId = bill.Id;
      guest.PaysRecreationFee = paysFeeByGuestId[guest.Id];
      guest.CheckOutAt = guestCheckOutAt;
    }

    foreach (ReservationSpotItem spot in linkedSpotItems)
    {
      spot.BillId = bill.Id;
      spot.HasGivenKey = true;
    }

    foreach (NewGuestInput input in command.NewGuests)
    {
      context.Guests.Add(new Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = command.ReservationId,
        BillId = bill.Id,
        PaysRecreationFee = input.PaysRecreationFee,
        FirstName = input.FirstName,
        LastName = input.LastName,
        NationalityId = input.NationalityId,
        DateOfBirth = input.DateOfBirth,
        DocumentType = input.DocumentType,
        DocumentNumber = input.DocumentNumber,
        Address = input.Address,
        ReasonOfStay = input.ReasonOfStay,
        StayDateRange = new DateRange(input.StayFrom, input.StayTo),
        Scartation = input.StayTo.AddYears(retentionSettings.Value.GuestYears),
        VisaNumber = input.VisaNumber,
        Note = input.Note,
        CreatedAt = now,
        UpdatedAt = now,
        CheckOutAt = guestCheckOutAt,
      });
    }

    List<AccessCard> touchedCards = [];
    foreach (AccessCardInput card in command.AccessCards)
    {
      if (existingCardsByUid.TryGetValue(card.Uid, out AccessCard? existing))
      {
        existing.BillId = bill.Id;
        existing.Deposit = card.Deposit;
        existing.ValidUntil = card.ValidUntil;
        existing.IssuedAtUtc = now;
        existing.Note = card.Note;
        touchedCards.Add(existing);
      }
      else
      {
        var newCard = new AccessCard
        {
          Id = Guid.NewGuid(),
          Uid = card.Uid,
          BillId = bill.Id,
          Deposit = card.Deposit,
          ValidUntil = card.ValidUntil,
          IssuedAtUtc = now,
          Note = card.Note,
        };
        context.AccessCards.Add(newCard);
        touchedCards.Add(newCard);
      }
    }

    foreach (NewVehicleInput v in command.NewVehicles)
    {
      context.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = command.ReservationId,
        BillId = bill.Id,
        ServiceId = v.ServiceId,
        RegistrationNumber = v.RegistrationNumber,
      });
    }

    foreach (Vehicle vehicle in linkedVehicles)
    {
      vehicle.BillId = bill.Id;
    }

    bill.Raise(new BillCreatedDomainEvent(bill.Id));

    await context.SaveChangesAsync(cancellationToken);

    string realName = $"{command.Payer.Name} {command.Payer.Surname}".Trim();
    foreach (AccessCard touched in touchedCards)
    {
      await TryPushToGateAsync(touched, realName, cancellationToken);
    }

    return Result.Success(new CreateBillResponse(bill.Id, number));
  }

  private static async Task<decimal> ComputeInvoiceTotalAsync(
    IApplicationDbContext context, Guid invoiceId, CancellationToken cancellationToken) =>
    await context.InvoiceItems
      .Where(item => item.InvoiceId == invoiceId)
      .SumAsync(item => item.Quantity * item.UnitPrice, cancellationToken);

  private async Task TryPushToGateAsync(
    AccessCard card, string realName, CancellationToken cancellationToken)
  {
    // Unspecified Kind makes GetUtcOffset treat the value as local wall-clock for DST.
    var localEndOfDay = card.ValidUntil.ToDateTime(new TimeOnly(23, 59, 59));
    var validTo = new DateTimeOffset(localEndOfDay, CampTimeZone.GetUtcOffset(localEndOfDay));

    var payload = new GateCardPayload(validTo, realName, card.Note ?? string.Empty);

    try
    {
      await gateClient.PutCardAsync(card.Uid, payload, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex,
        "Gate webhook PUT failed for card uid {Uid} (bill {BillId}); DB write kept.",
        card.Uid, card.BillId);
    }
  }
}
