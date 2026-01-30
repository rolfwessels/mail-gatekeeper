using System.Net;

namespace MailGatekeeper.Api;

public sealed class ApiTokenMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, Settings settings)
  {
    // Health check stays open (useful for local probes)
    if (context.Request.Path.Equals("/health"))
    {
      await next(context);
      return;
    }

    var expected = settings.GatekeeperApiToken;
    if (string.IsNullOrWhiteSpace(expected))
    {
      context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
      await context.Response.WriteAsync("GATEKEEPER_API_TOKEN is not configured");
      return;
    }

    var auth = context.Request.Headers.Authorization.ToString();
    var ok = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
             && auth.Substring("Bearer ".Length).Trim().Equals(expected, StringComparison.Ordinal);

    if (!ok)
    {
      context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
      await context.Response.WriteAsync("unauthorized");
      return;
    }

    await next(context);
  }
}
