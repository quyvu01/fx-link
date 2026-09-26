using FxLink.Nats.Abstractions;
using FxLink.Nats.Registries;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace FxLink.Nats.Implementations;

internal sealed class NatsConnectionProvider : INatsConnectionProvider, IAsyncDisposable
{
    public NatsConnection Connection { get; }
    public NatsJSContext JetStream { get; }

    public NatsConnectionProvider(INatsConfiguration configuration)
    {
        var options = NatsOpts.Default with
        {
            Url = configuration.Url,
            Name = "FxLink",
            AuthOpts = new NatsAuthOpts
            {
                Username = configuration.UserName,
                Password = configuration.Password,
                Token = configuration.Token,
                CredsFile = configuration.CredsFile
            },
            TlsOpts = configuration.TlsOption ?? NatsTlsOpts.Default
        };

        // Connects lazily on first use, and reconnects automatically after a drop — nothing here
        // needs to (or should) dial eagerly.
        Connection = new NatsConnection(options);
        JetStream = new NatsJSContext(Connection);
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
