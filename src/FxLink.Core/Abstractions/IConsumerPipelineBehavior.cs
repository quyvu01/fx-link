namespace FxLink.Core.Abstractions;

public interface IConsumerPipelineBehavior;

public interface IConsumerPipelineBehavior<in TMessage> : IConsumerPipelineBehavior where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, Func<Task> next, CancellationToken token = default);
}