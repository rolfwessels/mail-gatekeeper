using Bumbershoot.Utilities;
using MailGatekeeper;
using MailGatekeeper.Api;
using MailGatekeeper.Api.Imap;
using MailGatekeeper.Api.Rules;
using Microsoft.AspNetCore.Http.HttpResults;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.WithJsonAndEnvVariables();

builder.Host.UseSerilog((ctx, cfg) =>
{
  cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console();
});

builder.Services.AddSingleton<Settings>();
builder.Services.AddSingleton<GatekeeperStore>();
builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddSingleton<ImapClientFactory>();
builder.Services.AddSingleton<ImapService>();
builder.Services.AddHostedService<PollingService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
  options.SwaggerDoc("v1", new() { Title = "Mail Gatekeeper API", Version = "v1" });
  options.AddSecurityDefinition("Bearer", new()
  {
    Name = "Authorization",
    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    Description = "Enter 'Bearer' [space] and then your token"
  });
  options.AddSecurityRequirement(new()
  {
    {
      new()
      {
        Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
      },
      Array.Empty<string>()
    }
  });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
  options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mail Gatekeeper API v1");
  options.RoutePrefix = string.Empty; // Serve Swagger UI at root
});

app.UseMiddleware<ApiTokenMiddleware>();

// Health (no auth)
app.MapGet("/health", () => Results.Ok(new { ok = true }));

// List alerts
app.MapGet("/v1/alerts", (GatekeeperStore store, Settings settings, int? limit) =>
{
  var take = Math.Clamp(limit ?? 20, 1, 200);
  return Results.Ok(store.GetAlerts(settings.DeduplicateThreads, settings.ThreadItemLimit).Take(take));
});

// Manual scan trigger
app.MapPost("/v1/scan", async (ImapService imap, CancellationToken ct) =>
{
  var result = await imap.ScanAsync(ct);
  return Results.Ok(result);
});

// Create draft reply
app.MapPost("/v1/drafts", async Task<Results<Ok<CreateDraftResponse>, BadRequest<string>>> (
  CreateDraftRequest req,
  ImapService imap,
  CancellationToken ct) =>
{
  if (string.IsNullOrWhiteSpace(req.AlertId))
    return TypedResults.BadRequest("alertId is required");
  if (string.IsNullOrWhiteSpace(req.Body))
    return TypedResults.BadRequest("body is required");

  var response = await imap.CreateDraftReplyAsync(req, ct);
  return TypedResults.Ok(response);
});

app.Run();

namespace MailGatekeeper
{
  public sealed record CreateDraftRequest(string AlertId, string Body, string? SubjectPrefix = null);

  public sealed record CreateDraftResponse(string DraftMessageId, string DraftsFolder, string InReplyTo);
}
