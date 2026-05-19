using Application.Abstractions.Email;
using Application.Reservations.EventHandlers;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Reservations.EventHandlers;

public sealed class GroupReservationCreatedEmailHandlerTests : HandlerTestBase
{
  private readonly IEmailTemplateRenderer _renderer = Substitute.For<IEmailTemplateRenderer>();
  private readonly IEmailSender _sender = Substitute.For<IEmailSender>();

  private GroupReservationCreatedEmailHandler CreateHandler() => new(
    Db,
    _renderer,
    _sender,
    NullLogger<GroupReservationCreatedEmailHandler>.Instance);

  [Fact]
  public async Task Handle_RendersTemplateWithGroupTokens()
  {
    GroupReservation group = new GroupReservationBuilder()
      .WithSecret("secret-abc")
      .WithOrganizer("Alice", "alice@example.com")
      .For(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10))
      .WithNote("Bus arrives 14:00")
      .WithLanguage("cs")
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync("group-reservation-invitation", "cs", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
      .Returns(new RenderedEmail("Subject", "Body"));

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
      "group-reservation-invitation",
      "cs",
      Arg.Is<IReadOnlyDictionary<string, string>>(d =>
        d["Id"] == group.Id.ToString()
        && d["Secret"] == "secret-abc"
        && d["OrganizerName"] == "Alice"
        && d["From"] == "2026-07-01"
        && d["To"] == "2026-07-10"
        && d["Note"] == "Bus arrives 14:00"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SendsToOrganizerEmail()
  {
    GroupReservation group = new GroupReservationBuilder()
      .WithOrganizer("Bob", "bob@example.com")
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None);

    await _sender.Received(1).SendAsync(
      Arg.Is<EmailMessage>(m => m.To == "bob@example.com" && m.Subject == "S" && m.Body == "B"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_UsesGroupLanguageForTemplate()
  {
    GroupReservation group = new GroupReservationBuilder()
      .WithLanguage("en")
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
      "group-reservation-invitation",
      "en",
      Arg.Any<IReadOnlyDictionary<string, string>>(),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_NullNote_RendersEmptyString()
  {
    GroupReservation group = new GroupReservationBuilder()
      .WithNote(null)
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
      Arg.Any<string>(),
      Arg.Any<string>(),
      Arg.Is<IReadOnlyDictionary<string, string>>(d => d["Note"] == string.Empty),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_WhenGroupMissing_DoesNotSend()
  {
    var missingId = Guid.NewGuid();

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(missingId), CancellationToken.None);

    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default!, default);
    await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
  }

  [Fact]
  public async Task Handle_WhenTemplateRenderFails_DoesNotSend()
  {
    GroupReservation group = new GroupReservationBuilder().Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(SharedKernel.Result.Failure<RenderedEmail>(
        SharedKernel.Error.Problem("Email.TemplateNotFound", "missing")));

    await CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None);

    await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
  }

  [Fact]
  public async Task Handle_WhenSendThrows_DoesNotPropagate()
  {
    GroupReservation group = new GroupReservationBuilder().Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(new RenderedEmail("S", "B"));
    _sender.WhenForAnyArgs(s => s.SendAsync(default!, default))
      .Do(_ => throw new InvalidOperationException("smtp down"));

    await Should.NotThrowAsync(() =>
      CreateHandler().Handle(new GroupReservationCreatedDomainEvent(group.Id), CancellationToken.None));
  }
}
