namespace FxLink.Core.Abstractions;

public interface IConsumerPipelineBehavior<in TMessage> where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, Func<Task> next, CancellationToken token = default);
}