using MailGatekeeper.Api.Imap;
using MimeKit;

namespace MailGatekeeper.Api.Tests.Imap;

[TestFixture]
[TestOf(typeof(ImapService))]
public class ImapServiceReplyTests
{
  [Test]
  public void BuildReply_SimpleEmail_FromSenderToMe_ReplyGoesToSender()
  {
    // Arrange: Someone sends me an email
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John Doe", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "me@example.com"));
    original.Subject = "Hello";
    original.MessageId = "<original@example.com>";
    
    var myEmail = "me@example.com";
    var replyBody = "Thanks for your email!";
    
    // Act: Build a reply
    var reply = BuildReplyPublic(original, myEmail, replyBody, null);
    
    // Assert: Reply should go from me to the original sender
    Assert.That(reply.From.Count, Is.EqualTo(1));
    Assert.That(reply.From.Mailboxes.First().Address, Is.EqualTo("me@example.com"));
    
    Assert.That(reply.To.Count, Is.EqualTo(1));
    Assert.That(reply.To.Mailboxes.First().Address, Is.EqualTo("john@example.com"));
    
    Assert.That(reply.Cc.Count, Is.EqualTo(0));
    Assert.That(reply.Subject, Is.EqualTo("Re: Hello"));
    Assert.That(reply.InReplyTo, Is.EqualTo("original@example.com")); // MimeKit strips angle brackets
  }
  
  [Test]
  public void BuildReply_EmailWithMultipleRecipients_ReplyAll_ExcludesMe()
  {
    // Arrange: Someone sends an email to me and others
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John Doe", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "me@example.com"));
    original.To.Add(new MailboxAddress("Jane Smith", "jane@example.com"));
    original.To.Add(new MailboxAddress("Bob Johnson", "bob@example.com"));
    original.Subject = "Team Discussion";
    original.MessageId = "<team@example.com>";
    
    var myEmail = "me@example.com";
    var replyBody = "I agree!";
    
    // Act: Build a reply-all
    var reply = BuildReplyPublic(original, myEmail, replyBody, null);
    
    // Assert: Reply should go to original sender + all other recipients (excluding me)
    Assert.That(reply.From.Count, Is.EqualTo(1));
    Assert.That(reply.From.Mailboxes.First().Address, Is.EqualTo("me@example.com"));
    
    Assert.That(reply.To.Count, Is.EqualTo(3));
    var toAddresses = reply.To.Mailboxes.Select(m => m.Address).ToList();
    Assert.That(toAddresses, Does.Contain("john@example.com")); // Original sender
    Assert.That(toAddresses, Does.Contain("jane@example.com")); // Other recipient
    Assert.That(toAddresses, Does.Contain("bob@example.com"));  // Other recipient
    Assert.That(toAddresses, Does.Not.Contain("me@example.com")); // Should NOT include myself
    
    Assert.That(reply.Cc.Count, Is.EqualTo(0));
  }
  
  [Test]
  public void BuildReply_EmailWithCc_CcIsPreserved_ExcludesMe()
  {
    // Arrange: Email with CC recipients
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John Doe", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "me@example.com"));
    original.Cc.Add(new MailboxAddress("Manager", "manager@example.com"));
    original.Cc.Add(new MailboxAddress("Also Me", "me@example.com")); // I'm also in CC
    original.Cc.Add(new MailboxAddress("Other", "other@example.com"));
    original.Subject = "Important";
    
    var myEmail = "me@example.com";
    var replyBody = "Got it!";
    
    // Act: Build a reply-all
    var reply = BuildReplyPublic(original, myEmail, replyBody, null);
    
    // Assert: CC should be preserved, excluding me
    Assert.That(reply.From.Mailboxes.First().Address, Is.EqualTo("me@example.com"));
    
    Assert.That(reply.To.Count, Is.EqualTo(1));
    Assert.That(reply.To.Mailboxes.First().Address, Is.EqualTo("john@example.com"));
    
    Assert.That(reply.Cc.Count, Is.EqualTo(2));
    var ccAddresses = reply.Cc.Mailboxes.Select(m => m.Address).ToList();
    Assert.That(ccAddresses, Does.Contain("manager@example.com"));
    Assert.That(ccAddresses, Does.Contain("other@example.com"));
    Assert.That(ccAddresses, Does.Not.Contain("me@example.com")); // Should NOT include myself in CC
  }
  
  [Test]
  public void BuildReply_EmailFromMyself_ShouldNotIncludeMeInTo()
  {
    // Arrange: I sent an email to someone (edge case - replying to my own email)
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("Me", "me@example.com"));
    original.To.Add(new MailboxAddress("John Doe", "john@example.com"));
    original.Subject = "Follow up";
    
    var myEmail = "me@example.com";
    var replyBody = "Another follow up";
    
    // Act: Build a reply
    var reply = BuildReplyPublic(original, myEmail, replyBody, null);
    
    // Assert: Reply should NOT include me in To (even though I was the original sender)
    Assert.That(reply.From.Mailboxes.First().Address, Is.EqualTo("me@example.com"));
    Assert.That(reply.To.Count, Is.EqualTo(1));
    Assert.That(reply.To.Mailboxes.First().Address, Is.EqualTo("john@example.com"));
    Assert.That(reply.To.Mailboxes.Select(m => m.Address), Does.Not.Contain("me@example.com"));
  }
  
  [Test]
  public void BuildReply_CaseInsensitiveEmailComparison()
  {
    // Arrange: Email addresses with different casing
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John Doe", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "ME@EXAMPLE.COM")); // Different case
    original.To.Add(new MailboxAddress("Jane", "jane@example.com"));
    
    var myEmail = "me@example.com"; // lowercase
    var replyBody = "Testing case sensitivity";
    
    // Act: Build a reply
    var reply = BuildReplyPublic(original, myEmail, replyBody, null);
    
    // Assert: Should exclude me regardless of case
    Assert.That(reply.To.Count, Is.EqualTo(2));
    var toAddresses = reply.To.Mailboxes.Select(m => m.Address.ToLowerInvariant()).ToList();
    Assert.That(toAddresses, Does.Contain("john@example.com"));
    Assert.That(toAddresses, Does.Contain("jane@example.com"));
    Assert.That(toAddresses, Does.Not.Contain("me@example.com"));
  }
  
  [Test]
  public void BuildReply_SubjectPrefix_CustomPrefix()
  {
    // Arrange
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "me@example.com"));
    original.Subject = "Meeting";
    
    // Act
    var reply = BuildReplyPublic(original, "me@example.com", "Yes", "Fwd:");
    
    // Assert
    Assert.That(reply.Subject, Is.EqualTo("Fwd: Meeting"));
  }
  
  [Test]
  public void BuildReply_SubjectPrefix_AlreadyHasRe_DoesNotDuplicate()
  {
    // Arrange
    var original = new MimeMessage();
    original.From.Add(new MailboxAddress("John", "john@example.com"));
    original.To.Add(new MailboxAddress("Me", "me@example.com"));
    original.Subject = "Re: Meeting";
    
    // Act
    var reply = BuildReplyPublic(original, "me@example.com", "Yes", null);
    
    // Assert
    Assert.That(reply.Subject, Is.EqualTo("Re: Meeting")); // Should not add another "Re:"
  }

  // Helper method to access the private BuildReply method
  private static MimeMessage BuildReplyPublic(MimeMessage original, string fromAddress, string body, string? subjectPrefix)
  {
    var method = typeof(ImapService).GetMethod("BuildReply", 
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    
    if (method == null)
      throw new InvalidOperationException("BuildReply method not found");
    
    return (MimeMessage)method.Invoke(null, new object?[] { original, fromAddress, body, subjectPrefix })!;
  }
}
