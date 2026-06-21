namespace FxLink.Core.Abstractions;

public interface IServer<in TMessage> where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token);
}