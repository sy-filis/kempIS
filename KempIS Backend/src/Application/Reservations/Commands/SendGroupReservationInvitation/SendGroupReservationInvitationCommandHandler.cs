using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.SendGroupReservationInvitation;

internal sealed class SendGroupReservationInvitationCommandHandler(
  IApplicationDbContext context,
  IEmailTemplateRenderer templateRenderer,
  IEmailSender emailSender)
  : ICommandHandler<SendGroupReservationInvitationCommand>
{
  private const string TemplateName = "group-reservation-invitation";

  public async Task<Result> Handle(
    SendGroupReservationInvitationCommand command,
    CancellationToken cancellationToken)
  {
    GroupReservation? group = await context.GroupReservations
      .FirstOrDefaultAsync(g => g.Id == command.GroupReservationId, cancellationToken);

    if (group is null)
    {
      return Result.Failure(GroupReservationErrors.NotFound(command.GroupReservationId));
    }

    Dictionary<string, string> values = new(StringComparer.Ordinal)
    {
      ["Id"] = group.Id.ToString(),
      ["Secret"] = group.Secret,
      ["OrganizerName"] = group.OrganizerName,
      ["From"] = group.Period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["To"] = group.Period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["Note"] = group.Note ?? string.Empty
    };

    Result<RenderedEmail> render = await templateRenderer.RenderAsync(
      TemplateName,
      command.Language,
      values,
      cancellationToken);

    if (render.IsFailure)
    {
      return Result.Failure(render.Error);
    }

    await emailSender.SendAsync(
      new EmailMessage(group.OrganizerEmail, render.Value.Subject, render.Value.Body),
      cancellationToken);

    return Result.Success();
  }
}
