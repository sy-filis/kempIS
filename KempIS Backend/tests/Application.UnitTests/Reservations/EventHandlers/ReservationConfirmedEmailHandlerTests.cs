using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Configuration;
using Application.Reservations.EventHandlers;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using TestUtilities.Builders;

namespace Application.UnitTests.Reservations.EventHandlers;

public sealed class ReservationConfirmedEmailHandlerTests : HandlerTestBase
{
  private readonly IEmailTemplateRenderer _renderer = Substitute.For<IEmailTemplateRenderer>();
  private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
  private readonly FrontendOptions _options = new() { BaseUrl = "https://app.example.com" };

  private ReservationConfirmedEmailHandler CreateHandler() => new(
    Db,
    _renderer,
    _sender,
    Options.Create(_options),
    NullLogger<ReservationConfirmedEmailHandler>.Instance);

  [Fact]
  public async Task Handle_RendersTemplateWithReservationTokens()
  {
    Reservation reservation = new ReservationBuilder()
      .WithNumber("R-2026/0017")
      .For(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8))
      .WithLanguage("cs")
      .Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync("reservation-confirmation", "cs", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
      .Returns(new RenderedEmail("Subject", "Body"));

    await CreateHandler().Handle(new ReservationConfirmedDomainEvent(reservation.Id), CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
      "reservation-confirmation",
      "cs",
      Arg.Is<IReadOnlyDictionary<string, string>>(d =>
        d["Number"] == "R-2026/0017" &&
        d["From"] == "2026-06-01" &&
        d["To"] == "2026-06-08" &&
        d["GuestLink"] == $"https://app.example.com/reservation/{reservation.Id}?secret={reservation.Secret}"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SendsToReservationMakerEmail()
  {
    Reservation reservation = new ReservationBuilder()
      .MadeBy("Jan", "Novak", "jan@example.com", "+420000000000")
      .Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));

    await CreateHandler().Handle(new ReservationConfirmedDomainEvent(reservation.Id), CancellationToken.None);

    await _sender.Received(1).SendAsync(
      Arg.Is<EmailMessage>(m => m.To == "jan@example.com" && m.Subject == "S" && m.Body == "B"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_TrimsTrailingSlashFromBaseUrl()
  {
    _options.BaseUrl = "https://app.example.com/";
    Reservation reservation = new ReservationBuilder().Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));

    await CreateHandler().Handle(new ReservationConfirmedDomainEvent(reservation.Id), CancellationToken.None);

    await _renderer.Received().RenderAsync(
      Arg.Any<string>(),
      Arg.Any<string>(),
      Arg.Is<IReadOnlyDictionary<string, string>>(d =>
        d["GuestLink"] == $"https://app.example.com/reservation/{reservation.Id}?secret={reservation.Secret}"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_WhenReservationMissing_DoesNotSend()
  {
    var missingId = Guid.NewGuid();

    await CreateHandler().Handle(new ReservationConfirmedDomainEvent(missingId), CancellationToken.None);

    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default!, default);
    await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
  }

  [Fact]
  public async Task Handle_WhenTemplateRenderFails_DoesNotSend()
  {
    Reservation reservation = new ReservationBuilder().Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(SharedKernel.Result.Failure<RenderedEmail>(
        SharedKernel.Error.Problem("Email.TemplateNotFound", "missing")));

    await CreateHandler().Handle(new ReservationConfirmedDomainEvent(reservation.Id), CancellationToken.None);

    await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
  }

  [Fact]
  public async Task Handle_WhenSendThrows_DoesNotPropagate()
  {
    Reservation reservation = new ReservationBuilder().Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));
    _sender.WhenForAnyArgs(s => s.SendAsync(default!, default))
      .Do(_ => throw new InvalidOperationException("smtp down"));

    await Should.NotThrowAsync(() =>
      CreateHandler().Handle(new ReservationConfirmedDomainEvent(reservation.Id), CancellationToken.None));
  }
}
