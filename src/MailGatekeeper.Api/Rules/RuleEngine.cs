using MailGatekeeper.Api;

namespace MailGatekeeper.Api.Rules;

public sealed class RuleEngine
{
  private readonly Settings _settings;

  public RuleEngine(Settings settings)
  {
    _settings = settings;
  }

  public Classification Classify(string from, string subject, string snippet)
  {
    var fromLower = (from ?? "").ToLowerInvariant();
    var subjectLower = (subject ?? "").ToLowerInvariant();

    // Ignore no-reply senders
    if (_settings.IgnoreSenderPatterns.Any(p => fromLower.Contains(p)))
      return new Classification("info_only", "no-reply sender");

    // Ignore bulk/newsletter patterns
    if (_settings.IgnoreSubjectPatterns.Any(p => subjectLower.Contains(p)))
      return new Classification("info_only", "bulk/newsletter pattern");

    // Flag action keywords in subject
    var matchedKeyword = _settings.ActionSubjectPatterns.FirstOrDefault(p => subjectLower.Contains(p));
    if (matchedKeyword != null)
      return new Classification("action_required", $"keyword: {matchedKeyword}");

    // Question in snippet (basic heuristic) - only when snippet fetching is enabled
    if (_settings.FetchBodySnippet && !string.IsNullOrEmpty(snippet) && snippet.Contains('?'))
      return new Classification("action_required", "question in body");

    return new Classification("info_only", "no action signals");
  }
}

public sealed record Classification(string Category, string Reason);
