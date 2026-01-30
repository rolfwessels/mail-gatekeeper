using Bumbershoot.Utilities;

namespace MailGatekeeper.Api;

public class Settings(IConfiguration configuration) : BaseSettingsWithEncryption(configuration, "mail", "mg_key")
{
  // IMAP settings
  public string ImapHost => ReadConfigValue("ImapHost", "imap.gmail.com");
  public int ImapPort => ReadConfigValue("ImapPort", 993);
  public bool ImapUseSsl => ReadConfigValue("ImapUseSsl", true);
  public string ImapUsername => ReadConfigValue("ImapUsername", "");
  public string ImapPassword => ReadConfigValue("ImapPassword", "");
  public string ImapInboxFolder => ReadConfigValue("ImapInboxFolder", "INBOX");
  public string ImapDraftsFolder => ReadConfigValue("ImapDraftsFolder", "Drafts");

  // API Token
  public string GatekeeperApiToken => ReadConfigValue("GatekeeperApiToken", "asdlkjfaslkdjsadfasdfasd");

  // Polling settings
  public string GatekeeperCron => ReadConfigValue("GatekeeperCron", "0 * * * *");
  public bool ScanOnStart => ReadConfigValue("ScanOnStart", true);

  // Scan settings
  public int ScanLimit => ReadConfigValue("ScanLimit", 50);
  public bool FetchBodySnippet => ReadConfigValue("FetchBodySnippet", false);
}
