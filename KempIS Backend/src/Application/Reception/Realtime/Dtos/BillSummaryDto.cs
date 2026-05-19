namespace Application.Reception.Realtime.Dtos;

public sealed record BillSummaryDto(
  Guid BillId,
  string Number,
  string PayerDisplayName,
  DateOnly CheckInAt,
  DateOnly CheckOutAt,
  decimal Total,
  string Currency,
  IReadOnlyList<BillLineDto> Lines);
