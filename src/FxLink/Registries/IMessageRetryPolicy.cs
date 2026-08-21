namespace FxLink.Registries;

public interface IMessageRetryPolicy : IOption
{
    IMessageRetryPolicy Intervals(params TimeSpan[] intervals);
    IMessageRetryPolicy Ignore<TException>() where TException : Exception;
}