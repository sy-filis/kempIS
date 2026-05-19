namespace Application.Reception.Realtime.Dtos;

public sealed record BillLineDto(string Label, int Quantity, decimal UnitPrice, decimal Total);
