namespace MailGatekeeper.Rules;

public sealed class RuleEngine
{
  private static readonly string[] IgnoreSenderPatterns =
  {
    "no-reply",
    "noreply",
    "donotreply",
  };

  private static readonly string[] IgnoreSubjectPatterns =
  {
    "newsletter",
    "unsubscribe",
    "no-reply",
    "noreply",
    "do not reply",
  };

  private static readonly string[] ActionSubjectPatterns =
  {
    "action required",
    "urgent",
    "invoice",
    "payment",
    "overdue",
    "confirm",
    "verification",
    "reset password",
    "password reset",
    "meeting",
    "reschedule",
    "sign",
    "approve",
  };

  public Classification Classify(string from, string subject, string snippet)
  {
    var fromLower = (from ?? "").ToLowerInvariant();
    var subjectLower = (subject ?? "").ToLowerInvariant();

    // Ignore no-reply senders
    if (IgnoreSenderPatterns.Any(p => fromLower.Contains(p)))
      return new Classification("info_only", "no-reply sender");

    // Ignore bulk/newsletter patterns
    if (IgnoreSubjectPatterns.Any(p => subjectLower.Contains(p)))
      return new Classification("info_only", "bulk/newsletter pattern");

    // Flag action keywords in subject
    var matchedKeyword = ActionSubjectPatterns.FirstOrDefault(p => subjectLower.Contains(p));
    if (matchedKeyword != null)
      return new Classification("action_required", $"keyword: {matchedKeyword}");

    // Question in snippet (basic heuristic)
    if (!string.IsNullOrEmpty(snippet) && snippet.Contains('?'))
      return new Classification("action_required", "question in body");

    return new Classification("info_only", "no action signals");
  }
}

public sealed record Classification(string Category, string Reason);
