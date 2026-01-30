using MailKit;
using MimeKit;
using MailGatekeeper.Rules;

namespace MailGatekeeper.Imap;

public sealed class ImapService
{
  private readonly IConfiguration _config;
  private readonly GatekeeperStore _store;
  private readonly RuleEngine _rules;
  private readonly ImapClientFactory _factory;
  private readonly ILogger<ImapService> _log;

  public ImapService(
    IConfiguration config,
    GatekeeperStore store,
    RuleEngine rules,
    ImapClientFactory factory,
    ILogger<ImapService> log)
  {
    _config = config;
    _store = store;
    _rules = rules;
    _factory = factory;
    _log = log;
  }

  public async Task<ScanResult> ScanAsync(CancellationToken ct)
  {
    var opts = ImapOptions.FromConfig(_config);

    using var client = await _factory.ConnectAsync(opts, ct);
    var inbox = await client.GetFolderAsync(opts.InboxFolder, ct);
    await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

    var limit = int.TryParse(_config["SCAN_LIMIT"], out var n) ? n : 50;
    var start = Math.Max(0, inbox.Count - limit);

    var summaries = await inbox.FetchAsync(
      start, -1,
      MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
      ct);

    var added = 0;
    var fetchBody = bool.TryParse(_config["FETCH_BODY_SNIPPET"], out var fb) && fb;

    foreach (var summary in summaries.Reverse())
    {
      var env = summary.Envelope;
      if (env == null) continue;

      var from = env.From?.Mailboxes?.FirstOrDefault()?.ToString() ?? "(unknown)";
      var subject = env.Subject ?? "";
      var snippet = "";

      if (fetchBody)
      {
        try
        {
          var msg = await inbox.GetMessageAsync(summary.UniqueId, ct);
          snippet = ExtractSnippet(msg);
        }
        catch (Exception ex)
        {
          _log.LogWarning(ex, "Failed to fetch body for UID {Uid}", summary.UniqueId);
        }
      }

      var classification = _rules.Classify(from, subject, snippet);
      if (classification.Category != "action_required")
        continue;

      var messageId = env.MessageId ?? summary.UniqueId.Id.ToString();

      _store.Upsert(new Alert(
        Id: messageId,
        From: from,
        Subject: subject,
        ReceivedAt: env.Date ?? DateTimeOffset.UtcNow,
        Category: classification.Category,
        Reason: classification.Reason,
        Snippet: snippet,
        MessageId: messageId,
        Uid: summary.UniqueId.Id));

      added++;
    }

    return new ScanResult(summaries.Count, added);
  }

  public async Task<CreateDraftResponse> CreateDraftReplyAsync(CreateDraftRequest req, CancellationToken ct)
  {
    var opts = ImapOptions.FromConfig(_config);

    if (!_store.TryGet(req.AlertId, out var alert) || alert == null)
      throw new InvalidOperationException($"Unknown alertId: {req.AlertId}");

    using var client = await _factory.ConnectAsync(opts, ct);
    var inbox = await client.GetFolderAsync(opts.InboxFolder, ct);
    await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

    var uid = new UniqueId(alert.Uid);
    var original = await inbox.GetMessageAsync(uid, ct);

    var reply = BuildReply(original, opts.Username, req.Body, req.SubjectPrefix);

    var drafts = await client.GetFolderAsync(opts.DraftsFolder, ct);
    await drafts.OpenAsync(FolderAccess.ReadWrite, ct);
    await drafts.AppendAsync(reply, MessageFlags.Draft, ct);

    _log.LogInformation("Created draft reply to {MessageId} in {Folder}", alert.MessageId, opts.DraftsFolder);

    return new CreateDraftResponse(
      DraftMessageId: reply.MessageId ?? Guid.NewGuid().ToString("N"),
      DraftsFolder: opts.DraftsFolder,
      InReplyTo: reply.InReplyTo ?? "");
  }

  private static MimeMessage BuildReply(MimeMessage original, string fromAddress, string body, string? subjectPrefix)
  {
    var reply = new MimeMessage();

    reply.From.Add(new MailboxAddress("", fromAddress));
    reply.To.AddRange(original.From);

    var prefix = string.IsNullOrWhiteSpace(subjectPrefix) ? "Re: " : subjectPrefix.Trim() + " ";
    var originalSubject = original.Subject ?? "";
    reply.Subject = originalSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
      ? originalSubject
      : prefix + originalSubject;

    if (!string.IsNullOrWhiteSpace(original.MessageId))
    {
      reply.InReplyTo = original.MessageId;
      reply.References.Add(original.MessageId);
    }

    reply.Date = DateTimeOffset.UtcNow;
    reply.Body = new BodyBuilder { TextBody = body }.ToMessageBody();

    return reply;
  }

  private static string ExtractSnippet(MimeMessage msg, int maxLength = 280)
  {
    var text = (msg.TextBody ?? "")
      .Replace("\r", " ")
      .Replace("\n", " ")
      .Trim();

    return text.Length <= maxLength ? text : text[..maxLength];
  }
}

public sealed record ScanResult(int Scanned, int ActionRequired);
