using NATS.Client.Core;
using NATS.Client.JetStream;

namespace FxLink.Nats.Abstractions;

/// <summary>
/// Owns the single <see cref="NatsConnection"/> (and the JetStream context over it) shared by every
/// NATS transport component in this process. NatsConnection is thread-safe, multiplexes all
/// subscriptions over one socket, and reconnects on its own, so there is no benefit to each
/// component opening its own.
/// </summary>
internal interface INatsConnectionProvider
{
    NatsConnection Connection { get; }
    NatsJSContext JetStream { get; }
}
