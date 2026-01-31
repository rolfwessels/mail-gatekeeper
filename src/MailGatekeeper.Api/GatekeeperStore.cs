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
  uint Uid);

public sealed class GatekeeperStore
{
  // keyed by message-id (or UID fallback) to avoid duplicates
  private readonly ConcurrentDictionary<string, Alert> _alerts = new();

  public IEnumerable<Alert> GetAlerts(bool deduplicateThreads = true, int threadItemLimit = 1)
  {
    var alerts = _alerts.Values.OrderByDescending(a => a.ReceivedAt);

    if (!deduplicateThreads)
      return alerts;

    // Group by normalized subject and return only the latest N items from each thread
    return alerts
      .GroupBy(a => NormalizeSubject(a.Subject))
      .SelectMany(g => g.Take(threadItemLimit));
  }

  private static string NormalizeSubject(string subject)
  {
    if (string.IsNullOrWhiteSpace(subject))
      return string.Empty;

    var normalized = subject.Trim();

    // Remove common email prefixes
    while (true)
    {
      var original = normalized;

      if (normalized.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
        normalized = normalized.Substring(3).Trim();
      else if (normalized.StartsWith("RE:", StringComparison.OrdinalIgnoreCase))
        normalized = normalized.Substring(3).Trim();
      else if (normalized.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase))
        normalized = normalized.Substring(4).Trim();
      else if (normalized.StartsWith("FW:", StringComparison.OrdinalIgnoreCase))
        normalized = normalized.Substring(3).Trim();

      if (normalized == original)
        break;
    }

    return normalized.ToLowerInvariant();
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
