using FxLink.Configurators;

namespace FxLink.Registries;

internal sealed class MessageRetryPolicy : IMessageRetryPolicy
{
    internal TimeSpan[] RetryIntervals { get; private set; }
    internal Type[] IgnoreExceptions => _ignoreExceptions.ToArray();
    private readonly HashSet<Type> _ignoreExceptions = [];

    public IMessageRetryPolicy Intervals(params TimeSpan[] intervals)
    {
        RetryIntervals = intervals;
        return this;
    }

    public IMessageRetryPolicy Ignore<TException>() where TException : Exception
    {
        _ignoreExceptions.Add(typeof(TException));
        return this;
    }

    internal static MessageRetryPolicy DefaultMessageRetryPolicy =>
        new() { RetryIntervals = DistributedConfigurators.DefaultRetryPolicy };
}