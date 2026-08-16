namespace FxLink.Registries;

public interface IMessageRetryPolicy : IConsumeConfigurator
{
    IMessageRetryPolicy Intervals(params TimeSpan[] intervals);
    IMessageRetryPolicy Ignore<TException>() where TException : Exception;
}