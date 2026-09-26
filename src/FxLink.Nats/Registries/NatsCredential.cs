using NATS.Client.Core;

namespace FxLink.Nats.Registries;

public sealed class NatsCredential
{
    internal string UserNameValue { get; private set; }
    internal string PasswordValue { get; private set; }
    internal string TokenValue { get; private set; }
    internal string CredsFileValue { get; private set; }
    internal NatsTlsOpts TlsOptionValue { get; private set; }

    public void UserName(string userName) => UserNameValue = userName;
    public void Password(string password) => PasswordValue = password;
    public void Token(string token) => TokenValue = token;

    // Path to a .creds file (JWT + NKey seed), the usual auth for NGS / operator-mode servers.
    public void CredsFile(string path) => CredsFileValue = path;

    public void Tls(Action<NatsTlsOpts> tlsOption)
    {
        var option = new NatsTlsOpts();
        tlsOption.Invoke(option);
        TlsOptionValue = option;
    }
}
