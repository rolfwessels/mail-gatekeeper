using MailGatekeeper.Api.Rules;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace MailGatekeeper.Api.Imap;

public sealed class ImapService(
  Settings settings,
  GatekeeperStore store,
  RuleEngine rules,
  ImapClientFactory factory,
  ILogger<ImapService> log)
{
  public async Task<ScanResult> ScanAsync(CancellationToken ct)
  {
    var opts = ImapOptions.FromConfig(settings);

    using var client = await factory.ConnectAsync(opts, ct);

    var inbox = await client.GetFolderAsync(opts.InboxFolder, ct);
    await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

    var limit = settings.ScanLimit;
    var start = Math.Max(0, inbox.Count - limit);

    var summaries = await inbox.FetchAsync(
      start, -1,
      MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
      ct);

    var newAlerts = new List<Alert>();
    var fetchBody = settings.FetchBodySnippet;
    var includeRepliedThreads = settings.IncludeRepliedThreads;
    var userEmail = opts.Username.ToLowerInvariant();

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
          snippet = settings.FetchFullBody ? ExtractFullBody(msg) : ExtractSnippet(msg);
        }
        catch (Exception ex)
        {
          log.LogWarning(ex, "Failed to fetch body for UID {Uid}", summary.UniqueId);
        }
      }

      var classification = rules.Classify(from, subject, snippet);
      var isRepliedThread = false;

      // Check if this is a thread where the user has replied
      if (includeRepliedThreads && classification.Category != "action_required")
      {
        isRepliedThread = await HasUserRepliedInThreadAsync(inbox, summary, userEmail, ct);
      }

      if (classification.Category != "action_required" && !isRepliedThread)
        continue;

      var reason = isRepliedThread && classification.Category != "action_required"
        ? "thread with your reply"
        : classification.Reason;

      var messageId = env.MessageId ?? summary.UniqueId.Id.ToString();

      var alert = new Alert(
        Id: messageId,
        From: from,
        Subject: subject,
        ReceivedAt: env.Date ?? DateTimeOffset.UtcNow,
        Category: isRepliedThread && classification.Category != "action_required" ? "replied_thread" : classification.Category,
        Reason: reason,
        Snippet: snippet,
        Uid: summary.UniqueId.Id);

      // Track if this is a newly added alert
      if (store.Upsert(alert))
      {
        newAlerts.Add(alert);
      }
    }

    return new ScanResult(summaries.Count, newAlerts.Count, newAlerts);
  }

  public async Task<CreateDraftResponse> CreateDraftReplyAsync(CreateDraftRequest req, CancellationToken ct)
  {
    var opts = ImapOptions.FromConfig(settings);

    if (!store.TryGet(req.AlertId, out var alert) || alert == null)
      throw new InvalidOperationException($"Unknown alertId: {req.AlertId}");

    using var client = await factory.ConnectAsync(opts, ct);
    var inbox = await client.GetFolderAsync(opts.InboxFolder, ct);
    await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

    var uid = new UniqueId(alert.Uid);
    var original = await inbox.GetMessageAsync(uid, ct);

    var reply = BuildReply(original, opts.Username, req.Body, req.SubjectPrefix);

    // Try SpecialFolder.Drafts first (auto-detected), fallback to Gmail-specific, then config
    IMailFolder drafts;
    try
    {
      drafts = client.GetFolder(settings.ImapDraftsFolder);
      await drafts.OpenAsync(FolderAccess.ReadWrite, ct);
      log.LogInformation("Using auto-detected drafts folder: {Folder}", drafts.FullName);
    }
    catch
    {
      // Gmail-specific fallback
      var gmailDrafts = "[Gmail]/Drafts";
      try
      {
        drafts = await client.GetFolderAsync(gmailDrafts, ct);
        await drafts.OpenAsync(FolderAccess.ReadWrite, ct);
        log.LogInformation("Using Gmail-specific drafts folder: {Folder}", drafts.FullName);
      }
      catch
      {
        drafts = await client.GetFolderAsync(opts.DraftsFolder, ct);
        await drafts.OpenAsync(FolderAccess.ReadWrite, ct);
        log.LogInformation("Using configured drafts folder: {Folder}", drafts.FullName);
      }
    }

    log.LogInformation("Reply: From={From}, To={To}, Subject={Subject}",
      string.Join(",", reply.From),
      string.Join(",", reply.To),
      reply.Subject);
    await drafts.AppendAsync(reply, MessageFlags.Draft, ct);

    log.LogInformation("Created draft reply to {MessageId} in {Folder}", alert.Id, drafts.FullName);

    return new CreateDraftResponse(
      DraftMessageId: reply.MessageId ?? Guid.NewGuid().ToString("N"),
      DraftsFolder: opts.DraftsFolder,
      InReplyTo: reply.InReplyTo ?? "");
  }

  private static MimeMessage BuildReply(MimeMessage original, string fromAddress, string body, string? subjectPrefix)
  {
    var reply = new MimeMessage();

    reply.From.Add(new MailboxAddress("", fromAddress));

    // Reply-All: Add original From
    reply.To.AddRange(original.From);

    // Reply-All: Add original To recipients (excluding self)
    foreach (var addr in original.To.Mailboxes)
    {
      if (!addr.Address.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))
        reply.To.Add(addr);
    }

    // Reply-All: Add original Cc recipients (excluding self)
    foreach (var addr in original.Cc.Mailboxes)
    {
      if (!addr.Address.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))
        reply.Cc.Add(addr);
    }

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

  private static string ExtractFullBody(MimeMessage msg)
  {
    return (msg.TextBody ?? "").Trim();
  }

  private async Task<bool> HasUserRepliedInThreadAsync(IMailFolder inbox, IMessageSummary summary, string userEmail, CancellationToken ct)
  {
    try
    {
      // Fetch the full message to check References and In-Reply-To headers
      var msg = await inbox.GetMessageAsync(summary.UniqueId, ct);

      // Check if user's email is in the References chain (indicates user participated in thread)
      if (msg.References != null && msg.References.Count > 0)
      {
        // Search for messages in inbox sent by user that are in this thread
        SearchQuery query = SearchQuery.FromContains(userEmail);
        foreach (var reference in msg.References)
        {
          query = SearchQuery.And(query, SearchQuery.HeaderContains("Message-ID", reference));
        }

        var userMessageUids = await inbox.SearchAsync(query, ct);
        if (userMessageUids.Count > 0)
        {
          log.LogDebug("Found user reply in thread for message {MessageId}", summary.Envelope.MessageId);
          return true;
        }
      }

      return false;
    }
    catch (Exception ex)
    {
      log.LogWarning(ex, "Failed to check if user replied in thread for UID {Uid}", summary.UniqueId);
      return false;
    }
  }
}

public sealed record ScanResult(int Scanned, int ActionRequired, List<Alert> NewAlerts);
