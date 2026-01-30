using Cronos;
using MailGatekeeper.Api.Imap;

namespace MailGatekeeper.Api;

public sealed class PollingService : BackgroundService
{
  private readonly ILogger<PollingService> _log;
  private readonly Settings _settings;
  private readonly ImapService _imap;

  public PollingService(ILogger<PollingService> log, Settings settings, ImapService imap)
  {
    _log = log;
    _settings = settings;
    _imap = imap;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var cronExpr = _settings.GatekeeperCron;

    CronExpression cron;
    try
    {
      cron = CronExpression.Parse(cronExpr);
    }
    catch (Exception ex)
    {
      _log.LogError(ex, "Invalid GATEKEEPER_CRON: {Cron}", cronExpr);
      throw;
    }

    _log.LogInformation("Polling service started. Schedule={Cron}", cronExpr);

    // Optional: scan immediately on startup
    if (_settings.ScanOnStart)
    {
      await RunScanAsync(stoppingToken);
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      var now = DateTimeOffset.UtcNow;
      var next = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);

      if (next == null)
      {
        _log.LogWarning("Cron produced no next occurrence; sleeping 1h");
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        continue;
      }

      var delay = next.Value - now;
      if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

      _log.LogDebug("Next scan at {NextUtc} (in {Delay})", next.Value, delay);
      await Task.Delay(delay, stoppingToken);

      await RunScanAsync(stoppingToken);
    }
  }

  private async Task RunScanAsync(CancellationToken ct)
  {
    try
    {
      var result = await _imap.ScanAsync(ct);
      _log.LogInformation("Scan completed: {Scanned} scanned, {ActionRequired} action_required",
        result.Scanned, result.ActionRequired);
    }
    catch (Exception ex)
    {
      _log.LogError(ex, "Scan failed");
    }
  }
}
