using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Guests;

public sealed class CreateGuestCommandValidatorTests
{
  private readonly CreateGuestCommandValidator _sut = new();

  private static CreateGuestCommand Cmd(
    DocumentType? type = DocumentType.Passport,
    string? documentNumber = "X1",
    string reasonOfStay = "tourism",
    DateRange? stayDateRange = null) => new(
      ReservationId: Guid.NewGuid(),
      BillId: null,
      PaysRecreationFee: null,
      FirstName: "A",
      LastName: "B",
      NationalityId: Guid.NewGuid(),
      DateOfBirth: new DateOnly(1990, 1, 1),
      DocumentType: type,
      DocumentNumber: documentNumber,
      Address: new Address(Guid.NewGuid(), "C", "12345", "S", "1"),
      ReasonOfStay: reasonOfStay,
      StayDateRange: stayDateRange ?? new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      VisaNumber: null,
      Note: null,
      Scartation: null,
      CheckInAt: null,
      CheckOutAt: null,
      SignaturePngBase64: null);

  [Fact]
  public void InvalidDocumentTypeEnum_IsFlagged()
  {
    _sut.TestValidate(Cmd(type: (DocumentType)99))
      .ShouldHaveValidationErrorFor(c => c.DocumentType);
  }

  [Fact]
  public void EmptyReasonOfStay_IsAccepted()
  {
    _sut.TestValidate(Cmd(reasonOfStay: ""))
      .ShouldNotHaveValidationErrorFor(c => c.ReasonOfStay);
  }

  [Fact]
  public void NullStayDateRange_IsAccepted()
  {
    CreateGuestCommand command = Cmd() with { StayDateRange = null };

    _sut.TestValidate(command)
      .ShouldNotHaveValidationErrorFor(c => c.StayDateRange);
  }

  [Fact]
  public void NullDocumentTypeAndNumber_IsAccepted()
  {
    TestValidationResult<CreateGuestCommand> result = _sut.TestValidate(Cmd(type: null, documentNumber: null));

    result.ShouldNotHaveValidationErrorFor(c => c.DocumentType);
    result.ShouldNotHaveValidationErrorFor(c => c.DocumentNumber);
  }

  [Fact]
  public void NullDocumentTypeWithNonNullDocumentNumber_IsRejected()
  {
    TestValidationResult<CreateGuestCommand> result = _sut.TestValidate(Cmd(type: null, documentNumber: "ABC"));

    result.IsValid.ShouldBeFalse();
  }

  [Fact]
  public void StayDateRange_RangeOutOfOrder_IsRejected_WhenProvided()
  {
    CreateGuestCommand command = Cmd() with
    {
      StayDateRange = new DateRange(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 5)),
    };

    _sut.TestValidate(command).ShouldNotHaveValidationErrorFor(c => c.StayDateRange);
  }

  [Fact]
  public void NullReservationId_DoesNotProduceReservationIdError()
  {
    CreateGuestCommand command = Cmd() with { ReservationId = null };

    TestValidationResult<CreateGuestCommand> result = _sut.TestValidate(command);

    result.ShouldNotHaveValidationErrorFor(c => c.ReservationId);
  }
}
