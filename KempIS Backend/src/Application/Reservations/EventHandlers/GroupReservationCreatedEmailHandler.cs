using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Reservations.EventHandlers;

internal sealed class GroupReservationCreatedEmailHandler(
  IApplicationDbContext context,
  IEmailTemplateRenderer templateRenderer,
  IEmailSender emailSender,
  ILogger<GroupReservationCreatedEmailHandler> logger)
  : IDomainEventHandler<GroupReservationCreatedDomainEvent>
{
  private const string TemplateName = "group-reservation-invitation";

  public async Task Handle(GroupReservationCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
  {
    GroupReservation? group = await context.GroupReservations
      .FirstOrDefaultAsync(g => g.Id == domainEvent.GroupReservationId, cancellationToken);

    if (group is null)
    {
      logger.LogWarning(
        "Skipping group reservation invitation email: group {GroupReservationId} not found",
        domainEvent.GroupReservationId);
      return;
    }

    Dictionary<string, string> values = new(StringComparer.Ordinal)
    {
      ["Id"] = group.Id.ToString(),
      ["Secret"] = group.Secret,
      ["OrganizerName"] = group.OrganizerName,
      ["From"] = group.Period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["To"] = group.Period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["Note"] = group.Note ?? string.Empty,
    };

    Result<RenderedEmail> rendered = await templateRenderer.RenderAsync(
      TemplateName,
      group.Language,
      values,
      cancellationToken);

    if (rendered.IsFailure)
    {
      logger.LogWarning(
        "Failed to render group reservation invitation email for group {GroupReservationId}: {Error}",
        group.Id,
        rendered.Error);
      return;
    }

    try
    {
      await emailSender.SendAsync(
        new EmailMessage(group.OrganizerEmail, rendered.Value.Subject, rendered.Value.Body),
        cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogWarning(
        ex,
        "Failed to send group reservation invitation email for group {GroupReservationId}",
        group.Id);
    }
  }
}
