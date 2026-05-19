using Application.Abstractions.Messaging;
using Application.IntegrationTests.Infrastructure;
using Application.Reservations.Commands.CancelReservation;
using Application.Reservations.Commands.CreateReservation;
using Application.Reservations.Commands.UpdateReservation;
using Application.Reservations.Queries.GetAvailability;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.IntegrationTests;

public sealed class DecoratorPipelineTests : IAsyncLifetime
{
  private PipelineFixture _fixture = null!;

  public Task InitializeAsync()
  {
    _fixture = new PipelineFixture();
    return Task.CompletedTask;
  }

  public async Task DisposeAsync() => await _fixture.DisposeAsync();

  [Fact]
  public async Task CreateReservation_InvalidCommand_ValidationShortCircuits_NoDbWrite()
  {
    using IServiceScope scope = _fixture.CreateScope();
    ICommandHandler<CreateReservationCommand, Guid> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateReservationCommand, Guid>>();

    CreateReservationCommand invalid = new(
      Name: string.Empty,
      Surname: string.Empty,
      Email: "not-an-email",
      Phone: string.Empty,
      From: default,
      To: default,
      SpotIds: [],
      Note: null,
      GroupReservationId: null,
      Services: Array.Empty<ReservationServiceLine>(),
      Vehicles: Array.Empty<ReservationVehicleLine>());

    Result<Guid> result = await pipeline.Handle(invalid, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBeOfType<ValidationError>();
    (await _fixture.Db.Reservations.AnyAsync()).ShouldBeFalse();
    await _fixture.AvailabilityChecker.DidNotReceiveWithAnyArgs()
      .CheckAsync(default!, default!, default!, default);
  }

  [Fact]
  public async Task CreateReservation_ValidCommand_FlowsThroughPipelineToDb()
  {
    using IServiceScope scope = _fixture.CreateScope();
    Guid spotGroupId = await SeedGroupAndSpot();

    _fixture.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    ICommandHandler<CreateReservationCommand, Guid> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateReservationCommand, Guid>>();

    Guid spotId = await _fixture.Db.Spots.Where(s => s.SpotGroupId == spotGroupId)
      .Select(s => s.Id).SingleAsync();

    CreateReservationCommand valid = new(
      Name: "Jan",
      Surname: "Novak",
      Email: "jan@example.com",
      Phone: "+420",
      From: new DateOnly(2026, 6, 1),
      To: new DateOnly(2026, 6, 3),
      SpotIds: [spotId],
      Note: null,
      GroupReservationId: null,
      Services: Array.Empty<ReservationServiceLine>(),
      Vehicles: Array.Empty<ReservationVehicleLine>());

    Result<Guid> result = await pipeline.Handle(valid, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await _fixture.Db.Reservations.CountAsync()).ShouldBe(1);
  }

  [Fact]
  public async Task UpdateReservation_InvalidEmptyIdAndReversedDates_ValidationBlocks()
  {
    using IServiceScope scope = _fixture.CreateScope();
    ICommandHandler<UpdateReservationCommand> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<UpdateReservationCommand>>();

    Result result = await pipeline.Handle(
      new UpdateReservationCommand(
        Id: Guid.Empty,
        Name: "Jan",
        Surname: "Novak",
        Email: "jan@example.com",
        Phone: "+420000",
        From: new DateOnly(2026, 6, 5),
        To: new DateOnly(2026, 6, 1),
        Note: null,
        GroupReservationId: null,
        SpotIds: [Guid.NewGuid()],
        Services: Array.Empty<ReservationServiceLine>(),
        Vehicles: Array.Empty<ReservationVehicleLine>()),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBeOfType<ValidationError>();
  }

  [Fact]
  public async Task CancelReservation_ValidButMissing_FlowsPastValidationToNotFoundError()
  {
    using IServiceScope scope = _fixture.CreateScope();
    ICommandHandler<CancelReservationCommand> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<CancelReservationCommand>>();
    var id = Guid.NewGuid();

    Result result = await pipeline.Handle(
      new CancelReservationCommand(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(id));
  }

  [Fact]
  public async Task Pipeline_SuccessCommand_EmitsInformationLogsForStartAndCompletion()
  {
    using IServiceScope scope = _fixture.CreateScope();
    await SeedReservation(ReservationState.Created);

    Guid id = await _fixture.Db.Reservations.Select(r => r.Id).SingleAsync();
    ICommandHandler<CancelReservationCommand> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<CancelReservationCommand>>();

    await pipeline.Handle(new CancelReservationCommand(id), CancellationToken.None);

    IReadOnlyCollection<ListLoggerProvider.LogEntry> entries = _fixture.Logs.Entries;
    entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("Processing command"));
    entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("Completed command"));
    entries.ShouldNotContain(e => e.Level == LogLevel.Error);
  }

  [Fact]
  public async Task Pipeline_FailureCommand_EmitsErrorLog_WithoutAlteringResult()
  {
    using IServiceScope scope = _fixture.CreateScope();
    ICommandHandler<CancelReservationCommand> pipeline =
      scope.ServiceProvider.GetRequiredService<ICommandHandler<CancelReservationCommand>>();
    var id = Guid.NewGuid();

    Result result = await pipeline.Handle(new CancelReservationCommand(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(id));
    _fixture.Logs.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("with error"));
  }

  [Fact]
  public async Task GetAvailabilityQuery_Pipeline_LogsAndReturnsSuccess()
  {
    using IServiceScope scope = _fixture.CreateScope();
    await SeedGroupAndSpot();

    IQueryHandler<GetAvailabilityQuery, AvailabilityResponse> pipeline =
      scope.ServiceProvider.GetRequiredService<IQueryHandler<GetAvailabilityQuery, AvailabilityResponse>>();

    Result<AvailabilityResponse> result = await pipeline.Handle(
      new GetAvailabilityQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.SpotGroups.Count.ShouldBe(1);
    _fixture.Logs.Entries.ShouldContain(e =>
      e.Level == LogLevel.Information && e.Message.Contains("Processing query"));
    _fixture.Logs.Entries.ShouldContain(e =>
      e.Level == LogLevel.Information && e.Message.Contains("Completed query"));
  }

  private async Task<Guid> SeedGroupAndSpot()
  {
    Domain.Reservations.SpotGroups.SpotGroup group = new TestUtilities.Builders.SpotGroupBuilder()
      .WithCapacity(5).Build();
    Domain.Reservations.Spots.Spot spot = new TestUtilities.Builders.SpotBuilder()
      .InGroup(group.Id).Build();
    _fixture.Db.SpotGroups.Add(group);
    _fixture.Db.Spots.Add(spot);
    await _fixture.Db.SaveChangesAsync();
    return group.Id;
  }

  private async Task SeedReservation(ReservationState state)
  {
    DomainReservation r = new TestUtilities.Builders.ReservationBuilder()
      .InState(state).Build();
    _fixture.Db.Reservations.Add(r);
    await _fixture.Db.SaveChangesAsync();
  }
}
