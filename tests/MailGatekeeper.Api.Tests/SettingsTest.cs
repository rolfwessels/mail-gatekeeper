using Bumbershoot.Utilities;
using Microsoft.Extensions.Configuration;

namespace MailGatekeeper.Api.Tests;

public class SettingsTest
{
  [Test]
  public void GivenConfigFiles_ShouldReadConfigSettings()
  {
    // arrange
    var env = "local";
    var configuration = new ConfigurationBuilder()
      .WithJsonAndEnvVariables(env)
      .Build();


    var apiSettings = new Settings(configuration);
    var secret = apiSettings.ImapUsername;
    Console.Out.WriteLine("ImapUsername: " + secret);
    Console.Out.WriteLine("ImapUsername: " + apiSettings.GetEncryptedValue(secret));
  }
}
