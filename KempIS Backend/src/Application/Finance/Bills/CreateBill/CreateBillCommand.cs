using Application.Abstractions.Messaging;
using Application.Finance.Bills.Shared;
using Domain.Finance.Payments;

namespace Application.Finance.Bills.CreateBill;

public sealed record CreateBillCommand(
  Guid? ReservationId,
  DateOnly CheckInAt,
  DateOnly CheckOutAt,
  BillPayerInput Payer,
  BillLegalEntityInput? LegalEntity,
  PaymentType PaymentType,
  Guid LanguageId,
  IReadOnlyList<BillItemInput> Items,
  IReadOnlyList<Guid> LinkedInvoiceIds,
  IReadOnlyList<ExistingGuestOnBillInput> ExistingGuests,
  IReadOnlyList<NewGuestInput> NewGuests,
  IReadOnlyList<Guid> ReservationSpotItemIds,
  IReadOnlyList<AccessCardInput> AccessCards,
  IReadOnlyList<NewVehicleInput> NewVehicles,
  IReadOnlyList<Guid> ExistingVehicleIds)
  : ICommand<CreateBillResponse>;

public sealed record CreateBillResponse(Guid BillId, string Number);
