namespace FxLink.Registries;

public interface IMessageRetryPolicy : IMessageConfigurator
{
    IMessageRetryPolicy Intervals(params TimeSpan[] intervals);
    IMessageRetryPolicy Ignore<TException>() where TException : Exception;
}