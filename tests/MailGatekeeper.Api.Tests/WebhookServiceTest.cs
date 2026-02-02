using AwesomeAssertions.Extensions;
using Bumbershoot.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MailGatekeeper.Api.Tests;

[TestFixture]
[TestOf(typeof(WebhookService))]
public class WebhookServiceTest
{
  [Test]
  [Explicit("Requires external HTTP endpoint")]
  public void WebhookService()
  {
    using var disposable = LoggerFactory.Create(builder => builder.AddConsole());


    var services = new ServiceCollection();

    services.AddHttpClient("weather", client => { client.BaseAddress = new Uri("https://example.com"); });

    var provider = services.BuildServiceProvider();

    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var config = ConfigurationFactory.Load();
    var webhookService = new WebhookService(new Settings(config), disposable.CreateLogger<WebhookService>(), factory);
    webhookService
      .NotifyAsync(
        [new Alert("asd", "asd", "asd", DateTime.Now.WithOffset(TimeSpan.FromHours(1)), "asd", "asd", "asd", 1)],
        CancellationToken.None).GetAwaiter().GetResult();
  }
}
