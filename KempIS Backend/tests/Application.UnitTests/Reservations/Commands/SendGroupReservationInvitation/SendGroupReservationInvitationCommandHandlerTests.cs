using Application.Abstractions.Email;
using Application.Reservations.Commands.SendGroupReservationInvitation;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Infrastructure.Database;
using Microsoft.Data.Sqlite;
using SharedKernel;

namespace Application.UnitTests.Reservations.Commands.SendGroupReservationInvitation;

public sealed class SendGroupReservationInvitationCommandHandlerTests : HandlerTestBase
{
  private readonly IEmailTemplateRenderer _renderer = Substitute.For<IEmailTemplateRenderer>();
  private readonly CapturingEmailSender _sender = new();

  private SendGroupReservationInvitationCommandHandler CreateSut() => new(Db, _renderer, _sender);

  private async Task<GroupReservation> SeedGroup(
      string organizerName = "Anna Organizer",
      string organizerEmail = "anna@example.com",
      string secret = "s3cret",
      string? note = "book early")
  {
    GroupReservation group = new GroupReservationBuilder()
      .For(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3))
      .WithOrganizer(organizerName, organizerEmail)
      .WithSecret(secret)
      .WithNote(note)
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();
    return group;
  }

  [Fact]
  public async Task Handle_GroupFound_RendersAndSendsEmailToOrganizer()
  {
    GroupReservation group = await SeedGroup();
    _renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success(new RenderedEmail("Invitation", "Body goes here")));

    Result result = await CreateSut().Handle(
        new SendGroupReservationInvitationCommand(group.Id, "en"),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _sender.Sent.Count.ShouldBe(1);
    _sender.Only.To.ShouldBe("anna@example.com");
    _sender.Only.Subject.ShouldBe("Invitation");
    _sender.Only.Body.ShouldBe("Body goes here");
  }

  [Fact]
  public async Task Handle_GroupMissing_ReturnsNotFound_NoEmailAttempted()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
        new SendGroupReservationInvitationCommand(missing, "en"),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.NotFound(missing));
    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default!, default);
    _sender.Sent.Count.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_RendererFails_ReturnsRenderError_NoEmailSent()
  {
    GroupReservation group = await SeedGroup();
    var renderError = Error.Failure("Template.NotFound", "template missing");
    _renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Failure<RenderedEmail>(renderError));

    Result result = await CreateSut().Handle(
        new SendGroupReservationInvitationCommand(group.Id, "en"),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(renderError);
    _sender.Sent.Count.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_TemplateValues_IncludeInvariantFormattedDatesAndSecret()
  {
    GroupReservation group = await SeedGroup(secret: "s3cret");
    _renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success(new RenderedEmail("s", "b")));

    await CreateSut().Handle(
        new SendGroupReservationInvitationCommand(group.Id, "cs"),
        CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
        "group-reservation-invitation",
        "cs",
        Arg.Is<IReadOnlyDictionary<string, string>>(v =>
            v["Id"] == group.Id.ToString()
            && v["Secret"] == "s3cret"
            && v["OrganizerName"] == "Anna Organizer"
            && v["From"] == "2026-05-01"
            && v["To"] == "2026-05-03"
            && v["Note"] == "book early"),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_NullNote_RendersAsEmptyString()
  {
    GroupReservation group = await SeedGroup(note: null);
    _renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success(new RenderedEmail("s", "b")));

    await CreateSut().Handle(
        new SendGroupReservationInvitationCommand(group.Id, "en"),
        CancellationToken.None);

    await _renderer.Received(1).RenderAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Is<IReadOnlyDictionary<string, string>>(v => v["Note"] == string.Empty),
        Arg.Any<CancellationToken>());
  }
}
