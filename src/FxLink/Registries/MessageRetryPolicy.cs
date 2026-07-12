namespace FxLink.Registries;

internal sealed class MessageRetryPolicy : IMessageRetryPolicy
{
    public IMessageRetryPolicy Intervals(params TimeSpan[] intervals)
    {
        throw new NotImplementedException();
    }

    public IMessageRetryPolicy Ignore<TException>() where TException : Exception
    {
        throw new NotImplementedException();
    }
}