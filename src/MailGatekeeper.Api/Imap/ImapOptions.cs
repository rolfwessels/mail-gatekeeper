namespace MailGatekeeper.Imap;

public sealed class ImapOptions
{
  public required string Host { get; init; }
  public int Port { get; init; } = 993;
  public bool UseSsl { get; init; } = true;
  public required string Username { get; init; }
  public required string Password { get; init; }

  public string InboxFolder { get; init; } = "INBOX";
  public string DraftsFolder { get; init; } = "Drafts";

  public static ImapOptions FromConfig(IConfiguration config)
  {
    return new ImapOptions
    {
      Host = config["IMAP_HOST"] ?? throw new InvalidOperationException("IMAP_HOST missing"),
      Port = int.TryParse(config["IMAP_PORT"], out var p) ? p : 993,
      UseSsl = bool.TryParse(config["IMAP_SSL"], out var ssl) ? ssl : true,
      Username = config["IMAP_USER"] ?? throw new InvalidOperationException("IMAP_USER missing"),
      Password = config["IMAP_PASS"] ?? throw new InvalidOperationException("IMAP_PASS missing"),
      InboxFolder = config["IMAP_INBOX"] ?? "INBOX",
      DraftsFolder = config["IMAP_DRAFTS"] ?? "Drafts",
    };
  }
}
