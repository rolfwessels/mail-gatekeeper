using Cronos;
using MailGatekeeper.Imap;

namespace MailGatekeeper;

public sealed class PollingService : BackgroundService
{
  private readonly ILogger<PollingService> _log;
  private readonly IConfiguration _config;
  private readonly ImapService _imap;

  public PollingService(ILogger<PollingService> log, IConfiguration config, ImapService imap)
  {
    _log = log;
    _config = config;
    _imap = imap;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var cronExpr = _config["GATEKEEPER_CRON"] ?? "0 * * * *";

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
    if (bool.TryParse(_config["SCAN_ON_START"], out var scanOnStart) && scanOnStart)
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
