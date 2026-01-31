using MailGatekeeper.Api;
using MailGatekeeper.Api.Rules;
using Microsoft.Extensions.Configuration;


namespace MailGatekeeper.Api.Tests.Rules;

public class RuleEngineTests
{
  private readonly Settings _settings;
  private readonly RuleEngine _engine;

  public RuleEngineTests()
  {
    // Create a test configuration with default values
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>())
      .Build();

    _settings = new Settings(configuration);
    _engine = new RuleEngine(_settings);
  }

  [Theory]
  [TestCase("no-reply@example.com", "Important Message", "Please read this")]
  [TestCase("noreply@company.org", "Your Order", "Your order has shipped")]
  [TestCase("donotreply@service.com", "Notification", "You have a new message")]
  [TestCase("info@business.com", "Update Available", "A new version is available")]
  public void Classify_NoReplySender_ReturnsInfoOnly(string from, string subject, string snippet)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no-reply sender");
  }

  [Theory]
  [TestCase("user@example.com", "Monthly Newsletter", "")]
  [TestCase("sender@company.org", "Unsubscribe from our list", "")]
  [TestCase("someone@service.com", "This is a no-reply message", "")]
  [TestCase("person@business.com", "noreply notification", "")]
  [TestCase("contact@site.com", "Please do not reply", "")]
  public void Classify_BulkOrNewsletterSubject_ReturnsInfoOnly(string from, string subject, string snippet)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("bulk/newsletter pattern");
  }

  [Theory]
  [TestCase("user@example.com", "ACTION REQUIRED: Verify your account", "", "action required")]
  [TestCase("sender@company.org", "Urgent: Server is down", "", "urgent")]
  [TestCase("someone@service.com", "Invoice #12345", "", "invoice")]
  [TestCase("person@business.com", "Payment due today", "", "payment")]
  [TestCase("contact@site.com", "Overdue notice", "", "overdue")]
  [TestCase("user@example.com", "Please confirm your email", "", "confirm")]
  [TestCase("contact@site.com", "Meeting tomorrow at 3pm", "", "meeting")]
  [TestCase("user@example.com", "Need to reschedule", "", "reschedule")]
  [TestCase("sender@company.org", "Please sign document attached", "", "sign document")]
  [TestCase("sender@company.org", "Signature required for this contract", "", "signature required")]
  [TestCase("someone@service.com", "Waiting for your approve", "", "approve")]
  public void Classify_ActionKeywordInSubject_ReturnsActionRequired(string from,
    string subject,
    string snippet,
    string expectedKeyword)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("action_required");
    result.Reason.Should().Be($"keyword: {expectedKeyword}");
  }

  [Theory]
  [TestCase("user@example.com", "Quick question", "Can you help me with this?")]
  [TestCase("sender@company.org", "Project status", "What's the status of the project?")]
  [TestCase("someone@service.com", "Feedback needed", "Do you have time to review?")]
  public void Classify_QuestionInSnippet_ReturnsActionRequired(string from, string subject, string snippet)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("action_required");
    result.Reason.Should().Be("question in body");
  }

  [Theory]
  [TestCase("user@example.com", "Weekly report", "Here is this week's report.")]
  [TestCase("sender@company.org", "FYI", "Just keeping you in the loop.")]
  [TestCase("someone@service.com", "Heads up", "Thought you should know about this.")]
  [TestCase("person@business.com", "", "")]
  public void Classify_NoActionSignals_ReturnsInfoOnly(string from, string subject, string snippet)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no action signals");
  }

  [Test]
  public void Classify_NullInputs_ReturnsInfoOnly()
  {
    // Act
    var result = _engine.Classify(null!, null!, null!);

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no action signals");
  }

  [Test]
  public void Classify_EmptyInputs_ReturnsInfoOnly()
  {
    // Act
    var result = _engine.Classify("", "", "");

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no action signals");
  }

  [Test]
  public void Classify_CaseInsensitive_NoReplySender()
  {
    // Act
    var result1 = _engine.Classify("NO-REPLY@EXAMPLE.COM", "Test", "");
    var result2 = _engine.Classify("NoReply@Example.Com", "Test", "");
    var result3 = _engine.Classify("DoNotReply@EXAMPLE.COM", "Test", "");

    // Assert
    result1.Category.Should().Be("info_only");
    result1.Reason.Should().Be("no-reply sender");
    result2.Category.Should().Be("info_only");
    result2.Reason.Should().Be("no-reply sender");
    result3.Category.Should().Be("info_only");
    result3.Reason.Should().Be("no-reply sender");
  }

  [Test]
  public void Classify_CaseInsensitive_ActionKeywords()
  {
    // Act
    var result1 = _engine.Classify("user@example.com", "ACTION REQUIRED", "");
    var result2 = _engine.Classify("user@example.com", "Action Required", "");
    var result3 = _engine.Classify("user@example.com", "action required", "");

    // Assert
    result1.Category.Should().Be("action_required");
    result2.Category.Should().Be("action_required");
    result3.Category.Should().Be("action_required");
  }

  [Test]
  public void Classify_PriorityOrder_NoReplySenderTakesPrecedence()
  {
    // Even with action keyword in subject, no-reply sender should still be info_only
    // Act
    var result = _engine.Classify("no-reply@example.com", "ACTION REQUIRED: Urgent", "Can you help?");

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no-reply sender");
  }

  [Test]
  public void Classify_PriorityOrder_BulkPatternTakesPrecedenceOverActionKeywords()
  {
    // Bulk pattern in subject should be info_only even if there are action keywords
    // Act
    var result = _engine.Classify("user@example.com", "Newsletter - Action Required", "");

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("bulk/newsletter pattern");
  }

  [Test]
  public void Classify_PriorityOrder_SubjectKeywordTakesPrecedenceOverQuestion()
  {
    // Action keyword in subject takes precedence over question in snippet
    // Act
    var result = _engine.Classify("user@example.com", "Invoice #12345", "Can you confirm?");

    // Assert
    result.Category.Should().Be("action_required");
    result.Reason.Should().Be("keyword: invoice");
  }

  [Test]
  public void Classify_PartialMatch_NoReplySender()
  {
    // Act
    var result = _engine.Classify("hello-no-reply-test@example.com", "Subject", "");

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no-reply sender");
  }

  [Test]
  public void Classify_PartialMatch_ActionKeyword()
  {
    // Act
    var result = _engine.Classify("user@example.com", "Re: Meeting notes from yesterday", "");

    // Assert
    result.Category.Should().Be("action_required");
    result.Reason.Should().Be("keyword: meeting");
  }

  [Theory]
  [TestCase("regular@example.com", "Normal email", "")]
  [TestCase("support@company.com", "Ticket closed", "")]
  [TestCase("team@business.org", "Project update", "Everything is going well.")]
  public void Classify_RegularEmails_WithoutActionSignals_ReturnsInfoOnly(string from, string subject, string snippet)
  {
    // Act
    var result = _engine.Classify(from, subject, snippet);

    // Assert
    result.Category.Should().Be("info_only");
    result.Reason.Should().Be("no action signals");
  }

  [Test]
  public void Classification_Record_HasCorrectProperties()
  {
    // Arrange
    var category = "action_required";
    var reason = "test reason";

    // Act
    var classification = new Classification(category, reason);

    // Assert
    classification.Category.Should().Be(category);
    classification.Reason.Should().Be(reason);
  }

  [Test]
  public void Classification_Record_SupportsEquality()
  {
    // Arrange
    var c1 = new Classification("action_required", "test");
    var c2 = new Classification("action_required", "test");
    var c3 = new Classification("info_only", "test");

    // Assert
    c1.Should().Be(c2);
    c1.Should().NotBe(c3);
  }

  [Test]
  public void Classify_CustomPatterns_CanBeConfigured()
  {
    // Arrange - Create custom settings
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["IgnoreSenderPatterns"] = "spam,junk",
        ["IgnoreSubjectPatterns"] = "advertisement,promo",
        ["ActionSubjectPatterns"] = "deadline,asap,important"
      })
      .Build();

    var customSettings = new Settings(configuration);
    var customEngine = new RuleEngine(customSettings);

    // Act & Assert - Custom ignore sender
    var result1 = customEngine.Classify("spam@example.com", "Test", "");
    result1.Category.Should().Be("info_only");
    result1.Reason.Should().Be("no-reply sender");

    // Act & Assert - Custom ignore subject
    var result2 = customEngine.Classify("user@example.com", "Special Advertisement", "");
    result2.Category.Should().Be("info_only");
    result2.Reason.Should().Be("bulk/newsletter pattern");

    // Act & Assert - Custom action keyword
    var result3 = customEngine.Classify("user@example.com", "ASAP response needed", "");
    result3.Category.Should().Be("action_required");
    result3.Reason.Should().Be("keyword: asap");

    // Act & Assert - Default patterns should not match
    var result4 = customEngine.Classify("no-reply@example.com", "Test", "");
    result4.Category.Should().Be("info_only");
    result4.Reason.Should().Be("no action signals");
  }
}
