namespace FxLink.Core.Abstractions;

public interface IPublisherPipelineBehavior<in TMessage> where TMessage : class
{
    Task PublishAsync(TMessage message, IPublisherContext context, Func<Task> next, CancellationToken token = default);
}