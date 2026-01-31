namespace MailGatekeeper.Api.Imap;

public sealed class ImapOptions
{
  public required string Host { get; init; }
  public int Port { get; init; } = 993;
  public bool UseSsl { get; init; } = true;
  public required string Username { get; init; }
  public required string Password { get; init; }

  public string InboxFolder { get; init; } = "INBOX";
  public string DraftsFolder { get; init; } = "[Gmail]/Drafts";

  public static ImapOptions FromConfig(Settings settings)
  {
    return new ImapOptions
    {
      Host = settings.ImapHost ?? throw new InvalidOperationException("IMAP_HOST missing"),
      Port = settings.ImapPort,
      UseSsl = settings.ImapUseSsl,
      Username = settings.ImapUsername ?? throw new InvalidOperationException("IMAP_USER missing"),
      Password = settings.ImapPassword ?? throw new InvalidOperationException("IMAP_PASS missing"),
      InboxFolder = settings.ImapInboxFolder,
      DraftsFolder = settings.ImapDraftsFolder,
    };
  }
}
