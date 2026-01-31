using Cronos;
using MailGatekeeper.Api.Imap;

namespace MailGatekeeper.Api;

public sealed class PollingService(ILogger<PollingService> log, Settings settings, ImapService imap)
  : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var cronExpr = settings.GatekeeperCron;

    CronExpression cron;
    try
    {
      cron = CronExpression.Parse(cronExpr);
    }
    catch (Exception ex)
    {
      log.LogError(ex, "Invalid GATEKEEPER_CRON: {Cron}", cronExpr);
      throw;
    }

    log.LogInformation("Polling service started. Schedule={Cron}", cronExpr);

    // Optional: scan immediately on startup
    if (settings.ScanOnStart)
    {
      await RunScanAsync(stoppingToken);
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      var now = DateTimeOffset.UtcNow;
      var next = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);

      if (next == null)
      {
        log.LogWarning("Cron produced no next occurrence; sleeping 1h");
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        continue;
      }

      var delay = next.Value - now;
      if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

      log.LogDebug("Next scan at {NextUtc} (in {Delay})", next.Value, delay);
      await Task.Delay(delay, stoppingToken);

      await RunScanAsync(stoppingToken);
    }
  }

  private async Task RunScanAsync(CancellationToken ct)
  {
    try
    {
      var result = await imap.ScanAsync(ct);
      log.LogInformation("Scan completed: {Scanned} scanned, {ActionRequired} action_required",
        result.Scanned, result.ActionRequired);
    }
    catch (Exception ex)
    {
      log.LogError(ex, "Scan failed");
    }
  }
}
