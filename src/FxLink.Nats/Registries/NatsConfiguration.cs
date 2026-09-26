using NATS.Client.Core;

namespace FxLink.Nats.Registries;

internal sealed class NatsConfiguration(
    string url,
    string userName,
    string password,
    string token,
    string credsFile,
    NatsTlsOpts tlsOption,
    string streamName,
    string subjectPrefix,
    bool autoProvision,
    int maxDeliver,
    TimeSpan ackWait,
    int maxAckPending,
    TimeSpan maxAge)
    : INatsConfiguration
{
    public string Url { get; } = url;
    public string UserName { get; } = userName;
    public string Password { get; } = password;
    public string Token { get; } = token;
    public string CredsFile { get; } = credsFile;
    public NatsTlsOpts TlsOption { get; } = tlsOption;
    public string StreamName { get; } = streamName;
    public string SubjectPrefix { get; } = subjectPrefix;
    public bool AutoProvision { get; } = autoProvision;
    public int MaxDeliver { get; } = maxDeliver;
    public TimeSpan AckWait { get; } = ackWait;
    public int MaxAckPending { get; } = maxAckPending;
    public TimeSpan MaxAge { get; } = maxAge;
}
