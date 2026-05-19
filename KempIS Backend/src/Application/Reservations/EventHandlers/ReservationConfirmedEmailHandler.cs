using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Configuration;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Reservations.EventHandlers;

internal sealed class ReservationConfirmedEmailHandler(
  IApplicationDbContext context,
  IEmailTemplateRenderer templateRenderer,
  IEmailSender emailSender,
  IOptions<FrontendOptions> frontendOptions,
  ILogger<ReservationConfirmedEmailHandler> logger)
  : IDomainEventHandler<ReservationConfirmedDomainEvent>
{
  private const string TemplateName = "reservation-confirmation";

  private readonly FrontendOptions _frontend = frontendOptions.Value;

  public async Task Handle(ReservationConfirmedDomainEvent domainEvent, CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == domainEvent.ReservationId, cancellationToken);

    if (reservation is null)
    {
      logger.LogWarning(
        "Skipping confirmation email: reservation {ReservationId} not found",
        domainEvent.ReservationId);
      return;
    }

    string baseUrl = _frontend.BaseUrl.TrimEnd('/');
    string guestLink = $"{baseUrl}/reservation/{reservation.Id}?secret={reservation.Secret}";

    Dictionary<string, string> values = new(StringComparer.Ordinal)
    {
      ["Number"] = reservation.Number,
      ["From"] = reservation.Period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["To"] = reservation.Period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      ["GuestLink"] = guestLink,
    };

    Result<RenderedEmail> rendered = await templateRenderer.RenderAsync(
      TemplateName,
      reservation.Language,
      values,
      cancellationToken);

    if (rendered.IsFailure)
    {
      logger.LogWarning(
        "Failed to render confirmation email for reservation {ReservationId}: {Error}",
        reservation.Id,
        rendered.Error);
      return;
    }

    try
    {
      await emailSender.SendAsync(
        new EmailMessage(reservation.ReservationMaker.Email, rendered.Value.Subject, rendered.Value.Body),
        cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogWarning(
        ex,
        "Failed to send confirmation email for reservation {ReservationId}",
        reservation.Id);
    }
  }
}
