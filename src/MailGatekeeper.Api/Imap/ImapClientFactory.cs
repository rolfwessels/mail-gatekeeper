using MailKit.Net.Imap;
using MailKit.Security;

namespace MailGatekeeper.Api.Imap;

public sealed class ImapClientFactory
{
  public async Task<ImapClient> ConnectAsync(ImapOptions opts, CancellationToken ct)
  {
    var client = new ImapClient();

    // Basic security: never accept invalid certs silently.
    client.ServerCertificateValidationCallback = (s, c, h, e) => e == System.Net.Security.SslPolicyErrors.None;

    var secureSocket = opts.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

    await client.ConnectAsync(opts.Host, opts.Port, secureSocket, ct);
    await client.AuthenticateAsync(opts.Username, opts.Password, ct);

    return client;
  }
}
