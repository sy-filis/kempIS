using Domain.Common;
using Domain.Reservations.Nationalities;
using SharedKernel;

namespace Domain.Reservations.Guests;

public sealed class Guest : Entity
{
  public Guid Id { get; set; }
  public Guid? ReservationId { get; set; }
  public Guid? BillId { get; set; }
  public bool? PaysRecreationFee { get; set; }
  public required string FirstName { get; set; }
  public required string LastName { get; set; }
  public required Guid NationalityId { get; set; }
  public Nationality? Nationality { get; set; }
  public required DateOnly DateOfBirth { get; set; }
  public DocumentType? DocumentType { get; set; }
  public string? DocumentNumber { get; set; }
  public required Address Address { get; set; }
  public required string ReasonOfStay { get; set; }
  public DateRange? StayDateRange { get; set; }
  public string? VisaNumber { get; set; }
  public string? Note { get; set; }
  public DateOnly? Scartation { get; set; }
  public DateTime? CheckInAt { get; set; }
  public DateTime? CheckOutAt { get; set; }
  public byte[]? SignaturePng { get; set; }
  public DateTime? SignatureCapturedAtUtc { get; set; }

  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
  public DateTime? ReportedAt { get; set; }
}
