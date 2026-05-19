using Application.Abstractions.Messaging;
using Application.Configuration;
using Application.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Retention;

internal sealed class RetentionScheduler(
  IServiceScopeFactory scopeFactory,
  IDateTimeProvider clock,
  IOptions<RetentionSettings> settings,
  ILogger<RetentionScheduler> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RunOnceAsync(stoppingToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.LogError(ex, "Retention sweep failed.");
      }

      TimeSpan delay = ComputeDelayUntilNextRun(
        clock.UtcNow, settings.Value.RunAtLocalTime, TimeZoneInfo.Local);

      try
      {
        await Task.Delay(delay, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }
  }

  private async Task RunOnceAsync(CancellationToken cancellationToken)
  {
    using IServiceScope scope = scopeFactory.CreateScope();
    var today = DateOnly.FromDateTime(clock.UtcNow);

    ICommandHandler<RunGuestAnonymizationCommand, int> guests = scope.ServiceProvider
      .GetRequiredService<ICommandHandler<RunGuestAnonymizationCommand, int>>();
    ICommandHandler<RunBillAnonymizationCommand, int> bills = scope.ServiceProvider
      .GetRequiredService<ICommandHandler<RunBillAnonymizationCommand, int>>();
    ICommandHandler<RunInvoiceAnonymizationCommand, int> invoices = scope.ServiceProvider
      .GetRequiredService<ICommandHandler<RunInvoiceAnonymizationCommand, int>>();

    await guests.Handle(new RunGuestAnonymizationCommand(today), cancellationToken);
    await bills.Handle(new RunBillAnonymizationCommand(today), cancellationToken);
    await invoices.Handle(new RunInvoiceAnonymizationCommand(today), cancellationToken);
  }

  internal static TimeSpan ComputeDelayUntilNextRun(
    DateTime nowUtc,
    TimeOnly targetLocal,
    TimeZoneInfo timeZone)
  {
    DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
    DateTime targetToday = nowLocal.Date + targetLocal.ToTimeSpan();
    var targetUnspecified = DateTime.SpecifyKind(
      targetToday <= nowLocal ? targetToday.AddDays(1) : targetToday,
      DateTimeKind.Unspecified);
    DateTime targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetUnspecified, timeZone);
    return targetUtc - nowUtc;
  }
}
