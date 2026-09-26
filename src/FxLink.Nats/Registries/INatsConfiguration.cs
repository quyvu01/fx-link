using NATS.Client.Core;

namespace FxLink.Nats.Registries;

internal interface INatsConfiguration
{
    string Url { get; }
    string UserName { get; }
    string Password { get; }
    string Token { get; }
    string CredsFile { get; }
    NatsTlsOpts TlsOption { get; }
    string StreamName { get; }
    string SubjectPrefix { get; }
    bool AutoProvision { get; }
    int MaxDeliver { get; }
    TimeSpan AckWait { get; }
    int MaxAckPending { get; }
    TimeSpan MaxAge { get; }
}
