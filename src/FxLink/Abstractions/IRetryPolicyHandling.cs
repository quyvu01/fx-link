using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IRetryPolicyHandling<in TMessage> where TMessage : class
{
    Task HandleRetryPolicyAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default);
    Task HandleDeadLetterAsync(IConsumerContext<TMessage> ctx, CancellationToken token = default);
}