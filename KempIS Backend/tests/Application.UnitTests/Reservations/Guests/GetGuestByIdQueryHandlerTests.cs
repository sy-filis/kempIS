using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GetGuestByIdQueryHandlerTests : HandlerTestBase
{
  private GetGuestByIdQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public async Task Handle_ReturnsGuestWithAllFields_WhenFound()
  {
    var guestId = Guid.NewGuid();
    var reservationId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var nationalityId = Guid.NewGuid();
    Address address = Addr();
    var stayRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
    var scartation = new DateOnly(2030, 5, 5);
    var checkInAt = new DateTime(2026, 5, 1, 14, 0, 0, DateTimeKind.Utc);
    var checkOutAt = new DateTime(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc);
    var createdAt = new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc);
    var updatedAt = new DateTime(2026, 5, 1, 13, 0, 0, DateTimeKind.Utc);
    var reportedAt = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);
    var signatureCapturedAt = new DateTime(2026, 5, 1, 14, 5, 0, DateTimeKind.Utc);

    // CreatedAt and UpdatedAt are auto-stamped by ApplicationDbContext.StampGuestTimestamps;
    // drive both via two saves at different clock values to verify the projection reads them.
    Clock.Set(createdAt);
    var guest = new Guest
    {
      Id = guestId,
      ReservationId = reservationId,
      BillId = billId,
      PaysRecreationFee = true,
      FirstName = "Jane",
      LastName = "Smith",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.IdCard,
      DocumentNumber = "D1",
      Address = address,
      ReasonOfStay = "Holiday",
      StayDateRange = stayRange,
      VisaNumber = "V1",
      Note = "initial",
      Scartation = scartation,
      CheckInAt = checkInAt,
      CheckOutAt = checkOutAt,
      SignaturePng = [1, 2, 3, 4],
      SignatureCapturedAtUtc = signatureCapturedAt,
      ReportedAt = reportedAt,
    };
    Db.Guests.Add(guest);
    await Db.SaveChangesAsync();

    Clock.Set(updatedAt);
    guest.Note = "notes";
    await Db.SaveChangesAsync();

    Result<GuestDetailResponse> result = await CreateSut()
      .Handle(new GetGuestByIdQuery(guestId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GuestDetailResponse value = result.Value;
    value.Id.ShouldBe(guestId);
    value.ReservationId.ShouldBe(reservationId);
    value.BillId.ShouldBe(billId);
    value.PaysRecreationFee.ShouldBe(true);
    value.FirstName.ShouldBe("Jane");
    value.LastName.ShouldBe("Smith");
    value.NationalityId.ShouldBe(nationalityId);
    value.DateOfBirth.ShouldBe(new DateOnly(1990, 1, 1));
    value.DocumentType.ShouldBe(DocumentType.IdCard);
    value.DocumentNumber.ShouldBe("D1");
    value.Address.ShouldBe(address);
    value.ReasonOfStay.ShouldBe("Holiday");
    value.StayDateRange.ShouldBe(stayRange);
    value.VisaNumber.ShouldBe("V1");
    value.Note.ShouldBe("notes");
    value.Scartation.ShouldBe(scartation);
    value.CheckInAt.ShouldBe(checkInAt);
    value.CheckOutAt.ShouldBe(checkOutAt);
    value.HasSignature.ShouldBeTrue();
    value.SignatureCapturedAtUtc.ShouldBe(signatureCapturedAt);
    value.CreatedAt.ShouldBe(createdAt);
    value.UpdatedAt.ShouldBe(updatedAt);
    value.ReportedAt.ShouldBe(reportedAt);
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenMissing()
  {
    Result<GuestDetailResponse> result = await CreateSut()
      .Handle(new GetGuestByIdQuery(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Guest.NotFound");
  }
}
