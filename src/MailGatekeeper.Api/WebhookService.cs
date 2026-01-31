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

    // Build human-readable summary for the webhook text
    var textBuilder = new StringBuilder();
    textBuilder.AppendLine($"📬 Mail Gatekeeper: {alertList.Count} new alert(s)");
    
    foreach (var alert in alertList.Take(5)) // Limit to 5 in summary
    {
      var fromName = ExtractName(alert.From);
      textBuilder.AppendLine($"• [{alert.Category}] {fromName}: {alert.Subject}");
    }
    
    if (alertList.Count > 5)
    {
      textBuilder.AppendLine($"  ...and {alertList.Count - 5} more");
    }

    var payload = new OpenClawWakePayload(
      Text: textBuilder.ToString().Trim(),
      Mode: "now"
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

  private static string ExtractName(string from)
  {
    // Extract display name from "Name <email>" format, or return email
    if (string.IsNullOrWhiteSpace(from))
      return "(unknown)";
    
    var ltIndex = from.IndexOf('<');
    if (ltIndex > 0)
      return from[..ltIndex].Trim().Trim('"');
    
    return from;
  }
}

/// <summary>
/// OpenClaw /hooks/wake compatible payload
/// </summary>
public sealed record OpenClawWakePayload(
  string Text,
  string Mode
);
