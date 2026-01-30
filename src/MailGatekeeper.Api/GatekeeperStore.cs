using System.Collections.Concurrent;

namespace MailGatekeeper.Api;

public sealed record Alert(
  string Id,
  string From,
  string Subject,
  DateTimeOffset ReceivedAt,
  string Category,
  string Reason,
  string Snippet,
  string MessageId,
  uint Uid);

public sealed class GatekeeperStore
{
  // keyed by message-id (or UID fallback) to avoid duplicates
  private readonly ConcurrentDictionary<string, Alert> _alerts = new();

  public IEnumerable<Alert> GetAlerts()
  {
    return _alerts.Values
      .OrderByDescending(a => a.ReceivedAt);
  }

  public void Upsert(Alert alert)
  {
    _alerts[alert.Id] = alert;
  }

  public bool TryGet(string alertId, out Alert? alert)
  {
    return _alerts.TryGetValue(alertId, out alert);
  }
}
