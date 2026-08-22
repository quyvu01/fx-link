using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider) where TMessage : class
{
    internal async Task ExecuteAsync(IConsumeContext<TMessage> context, CancellationToken token = default)
    {
        var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
        if (consumerType is null)
        {
            var messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();
            consumerType = messageKeys.GetKeysByMessageType(typeof(TMessage))
                .FirstOrDefault();
        }
        
        if (serviceProvider.GetKeyedService<IBatchAccumulator<TMessage>>(consumerType) is { } accumulator)
        {
            await accumulator.AddAsync(context, token);
            return;
        }

        var consumer = serviceProvider.GetKeyedService<IConsumer<TMessage>>(consumerType);
        if (consumer is null) return;
        var pipelineBehaviors = serviceProvider
            .GetServices<IConsumerPipelineBehavior<TMessage>>();
        var func = pipelineBehaviors
            .Reverse()
            .Aggregate((ConsumerHandlerDelegate)(ct => consumer.ConsumeAsync(context, ct)),
                (acc, next) => ct => next.ConsumeAsync(context, acc, ct));
        await func.Invoke(token);
    }
}