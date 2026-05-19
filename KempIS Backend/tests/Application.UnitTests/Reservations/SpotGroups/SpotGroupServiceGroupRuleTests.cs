using Application.Reservations.SpotGroups;
using Domain.Reservations.SpotGroups;
using Domain.Services.Services;
using SharedKernel;

namespace Application.UnitTests.Reservations.SpotGroups;

public sealed class SpotGroupServiceGroupRuleTests : HandlerTestBase
{
  private static Service Svc(ServiceGroup group) => new()
  {
    Id = Guid.NewGuid(),
    ServiceGroup = group,
    ServiceTypeId = Guid.NewGuid(),
    VatRateId = Guid.NewGuid(),
    Name = "S",
    BasePrice = 100m,
    IsActive = true,
  };

  private static CreateSpotGroupCommand CreateCmd(Guid serviceId) => new(
    ServiceId: serviceId,
    Name: "G",
    Description: null,
    Capacity: 5,
    IsActive: true,
    ImageUrl: "https://example.com/img.jpg",
    DetailsUrl: "https://example.com/details");

  private static UpdateSpotGroupCommand UpdateCmd(Guid id, Guid serviceId) => new(
    Id: id,
    ServiceId: serviceId,
    Name: "G",
    Description: null,
    Capacity: 5,
    IsActive: true,
    ImageUrl: "https://example.com/img.jpg",
    DetailsUrl: "https://example.com/details");

  [Fact]
  public async Task Create_ServiceWithSpotsGroup_Succeeds()
  {
    Service svc = Svc(ServiceGroup.Spots);
    Db.Services.Add(svc);
    await Db.SaveChangesAsync();

    CreateSpotGroupCommandHandler sut = new(Db);

    Result<Guid> result = await sut.Handle(CreateCmd(svc.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Create_ServiceWithNonSpotsGroup_FailsWithServiceNotInSpotsGroup()
  {
    Service svc = Svc(ServiceGroup.Meals);
    Db.Services.Add(svc);
    await Db.SaveChangesAsync();

    CreateSpotGroupCommandHandler sut = new(Db);

    Result<Guid> result = await sut.Handle(CreateCmd(svc.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("SpotGroup.ServiceNotInSpotsGroup");
  }

  [Fact]
  public async Task Create_ServiceDoesNotExist_FailsWithServiceNotFound()
  {
    CreateSpotGroupCommandHandler sut = new(Db);

    Result<Guid> result = await sut.Handle(CreateCmd(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Services.NotFound");
  }

  [Fact]
  public async Task Update_ServiceWithSpotsGroup_Succeeds()
  {
    Service oldSvc = Svc(ServiceGroup.Spots);
    Service newSvc = Svc(ServiceGroup.Spots);
    Db.Services.AddRange(oldSvc, newSvc);

    SpotGroup existing = new()
    {
      Id = Guid.NewGuid(),
      ServiceId = oldSvc.Id,
      Name = "G",
      Capacity = 5,
      IsActive = true,
      ImageUrl = "https://example.com/img.jpg",
      DetailsUrl = "https://example.com/details",
    };
    Db.SpotGroups.Add(existing);
    await Db.SaveChangesAsync();

    UpdateSpotGroupCommandHandler sut = new(Db);

    Result result = await sut.Handle(UpdateCmd(existing.Id, newSvc.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Update_ServiceWithNonSpotsGroup_FailsWithServiceNotInSpotsGroup()
  {
    Service oldSvc = Svc(ServiceGroup.Spots);
    Service newSvc = Svc(ServiceGroup.Vehicles);
    Db.Services.AddRange(oldSvc, newSvc);

    SpotGroup existing = new()
    {
      Id = Guid.NewGuid(),
      ServiceId = oldSvc.Id,
      Name = "G",
      Capacity = 5,
      IsActive = true,
      ImageUrl = "https://example.com/img.jpg",
      DetailsUrl = "https://example.com/details",
    };
    Db.SpotGroups.Add(existing);
    await Db.SaveChangesAsync();

    UpdateSpotGroupCommandHandler sut = new(Db);

    Result result = await sut.Handle(UpdateCmd(existing.Id, newSvc.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("SpotGroup.ServiceNotInSpotsGroup");
  }
}
