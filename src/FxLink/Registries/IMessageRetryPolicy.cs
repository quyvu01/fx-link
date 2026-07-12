namespace FxLink.Registries;

public interface IMessageRetryPolicy
{
    IMessageRetryPolicy Intervals(params TimeSpan[] intervals);
    IMessageRetryPolicy Ignore<TException>() where TException : Exception;
}