using Bumbershoot.Utilities;

namespace MailGatekeeper.Api;

public class Settings(IConfiguration configuration) : BaseSettingsWithEncryption(configuration, "", "mg_key")
{
  // IMAP settings
  public string ImapHost => ReadConfigValue("ImapHost", "imap.gmail.com");
  public int ImapPort => ReadConfigValue("ImapPort", 993);
  public bool ImapUseSsl => ReadConfigValue("ImapUseSsl", true);

  public string ImapUsername => ReadConfigValue("ImapUsername", "");

  //use https://myaccount.google.com/apppasswords
  public string ImapPassword => ReadConfigValue("ImapPassword", "");
  public string ImapInboxFolder => ReadConfigValue("ImapInboxFolder", "INBOX");
  public string ImapDraftsFolder => ReadConfigValue("ImapDraftsFolder", "[Gmail]/Drafts");

  // API Token
  public string GatekeeperApiToken => ReadConfigValue("GatekeeperApiToken", "asdlkjfaslkdjsadfasdfasd");

  // Polling settings
  public string GatekeeperCron => ReadConfigValue("GatekeeperCron", "0 * * * *");
  public bool ScanOnStart => ReadConfigValue("ScanOnStart", true);

  // Scan settings
  public int ScanLimit => ReadConfigValue("ScanLimit", 50);
  public bool FetchBodySnippet => ReadConfigValue("FetchBodySnippet", true);
  public bool FetchFullBody => ReadConfigValue("FetchFullBody", false);
  public bool DeduplicateThreads => ReadConfigValue("DeduplicateThreads", true);
  public int ThreadItemLimit => ReadConfigValue("ThreadItemLimit", 6);
  public bool IncludeRepliedThreads => ReadConfigValue("IncludeRepliedThreads", true);

  // Rule Engine patterns
  public string[] IgnoreSenderPatterns => ReadConfigValue("IgnoreSenderPatterns", "no-reply,noreply,donotreply,info,mongodb.com,team.mongodb.com")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(p => p.ToLowerInvariant())
    .ToArray();

  public string[] IgnoreSubjectPatterns =>
    ReadConfigValue("IgnoreSubjectPatterns", "newsletter,unsubscribe,no-reply,noreply,do not reply")
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(p => p.ToLowerInvariant())
      .ToArray();

  public string[] ActionSubjectPatterns => ReadConfigValue("ActionSubjectPatterns",
      "action required,urgent,invoice,payment,overdue,confirm,meeting,reschedule,sign document,signature required,approve,maintenance")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(p => p.ToLowerInvariant())
    .ToArray();
}
