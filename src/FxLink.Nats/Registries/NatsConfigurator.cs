using FxLink.Nats.Constants;

namespace FxLink.Nats.Registries;

internal sealed class NatsConfigurator : INatsConfigurator
{
    private string _urlValue;
    private NatsCredential Credential { get; } = new();
    private string _streamNameValue = NatsConstants.DefaultStreamName;
    private string _subjectPrefixValue = NatsConstants.DefaultSubjectPrefix;
    private bool _autoProvisionValue = true;
    private int _maxDeliverValue = NatsConstants.DefaultMaxDeliver;
    private TimeSpan _ackWaitValue = NatsConstants.DefaultAckWait;
    private int _maxAckPendingValue = NatsConstants.DefaultMaxAckPending;
    private TimeSpan _maxAgeValue = NatsConstants.DefaultMaxAge;

    public void Server(string url, Action<NatsCredential> configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _urlValue = url;
        configure?.Invoke(Credential);
    }

    public void StreamName(string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        _streamNameValue = streamName;
    }

    // Becomes the first subject token, so it can't itself contain the token separator or wildcards.
    public void SubjectPrefix(string subjectPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectPrefix);
        if (subjectPrefix.AsSpan().IndexOfAny(".*> \t") >= 0)
            throw new ArgumentException("Subject prefix must be a single token: no '.', '*', '>' or whitespace.",
                nameof(subjectPrefix));
        _subjectPrefixValue = subjectPrefix;
    }

    public void AutoProvision(bool autoProvision) => _autoProvisionValue = autoProvision;

    public void MaxDeliver(int maxDeliver)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDeliver, 1);
        _maxDeliverValue = maxDeliver;
    }

    public void AckWait(TimeSpan ackWait)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ackWait, TimeSpan.Zero);
        _ackWaitValue = ackWait;
    }

    public void MaxAckPending(int maxAckPending)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAckPending, 1);
        _maxAckPendingValue = maxAckPending;
    }

    public void MaxAge(TimeSpan maxAge)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAge, TimeSpan.Zero);
        _maxAgeValue = maxAge;
    }

    internal INatsConfiguration ToConfiguration()
    {
        if (_urlValue is null)
            throw new InvalidOperationException("NATS server must be configured by calling Server(...).");
        return new NatsConfiguration(_urlValue, Credential.UserNameValue, Credential.PasswordValue,
            Credential.TokenValue, Credential.CredsFileValue, Credential.TlsOptionValue, _streamNameValue,
            _subjectPrefixValue, _autoProvisionValue, _maxDeliverValue, _ackWaitValue, _maxAckPendingValue, _maxAgeValue);
    }
}
