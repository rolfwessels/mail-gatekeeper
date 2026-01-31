using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MailGatekeeper.Api;

public sealed class WebhookService(Settings settings, ILogger<WebhookService> log, IHttpClientFactory httpClientFactory)
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
  };

  public async Task NotifyAsync(IEnumerable<Alert> alerts, CancellationToken ct)
  {
    var webhookUrl = settings.WebhookUrl;
    if (string.IsNullOrWhiteSpace(webhookUrl))
    {
      log.LogDebug("Webhook URL not configured, skipping notification");
      return;
    }

    var alertList = alerts.ToList();
    if (alertList.Count == 0)
    {
      log.LogDebug("No alerts to notify");
      return;
    }

    var payload = new WebhookPayload(
      Event: "alerts.new",
      Timestamp: DateTimeOffset.UtcNow,
      AlertCount: alertList.Count,
      Alerts: alertList.Select(a => new WebhookAlert(
        Id: a.Id,
        From: a.From,
        Subject: a.Subject,
        ReceivedAt: a.ReceivedAt,
        Category: a.Category,
        Reason: a.Reason,
        Snippet: a.Snippet
      )).ToList()
    );

    try
    {
      var client = httpClientFactory.CreateClient("Webhook");
      var json = JsonSerializer.Serialize(payload, JsonOptions);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
      {
        Content = content
      };

      // Add authorization header if token is configured
      var token = settings.WebhookToken;
      if (!string.IsNullOrWhiteSpace(token))
      {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      }

      var response = await client.SendAsync(request, ct);

      if (response.IsSuccessStatusCode)
      {
        log.LogInformation("Webhook notification sent: {Count} alerts to {Url}", alertList.Count, webhookUrl);
      }
      else
      {
        var body = await response.Content.ReadAsStringAsync(ct);
        log.LogWarning("Webhook notification failed: {StatusCode} {Body}", response.StatusCode, body);
      }
    }
    catch (Exception ex)
    {
      log.LogError(ex, "Failed to send webhook notification to {Url}", webhookUrl);
    }
  }
}

public sealed record WebhookPayload(
  string Event,
  DateTimeOffset Timestamp,
  int AlertCount,
  List<WebhookAlert> Alerts
);

public sealed record WebhookAlert(
  string Id,
  string From,
  string Subject,
  DateTimeOffset ReceivedAt,
  string Category,
  string Reason,
  string Snippet
);
